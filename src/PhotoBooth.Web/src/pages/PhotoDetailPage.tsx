import { useState, useEffect, useRef, useCallback } from 'react';
import { flushSync } from 'react-dom';
import { useParams, useNavigate } from 'react-router-dom';
import { TransformWrapper, TransformComponent } from 'react-zoom-pan-pinch';
import type { ReactZoomPanPinchContentRef } from 'react-zoom-pan-pinch';
import { getPhotoByCode, getPhotoImageUrl, getAllPhotos } from '../api/client';
import type { PhotoDto } from '../api/types';
import { ChevronLeftIcon, ChevronRightIcon, DownloadIcon, ShareIcon, DotsVerticalIcon, SpinnerIcon } from '../components/Icons';
import { useTranslation } from '../i18n/useTranslation';
import { useSwipeNavigation } from '../hooks/useSwipeNavigation';

type PhotoAction = 'idle' | 'loading' | 'expanded';

interface PhotoDetailPageProps {
  urlPrefix: string;
}

export function PhotoDetailPage({ urlPrefix }: PhotoDetailPageProps) {
  const { code } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [photo, setPhoto] = useState<PhotoDto | null>(null);
  const [loading, setLoading] = useState(!!code);
  const [error, setError] = useState<string | null>(code ? null : t('photoNotFoundError'));
  const [allCodes, setAllCodes] = useState<string[]>([]);
  const [photoAction, setPhotoAction] = useState<PhotoAction>('idle');
  const [isZoomed, setIsZoomed] = useState(false);
  const cachedBlob = useRef<Blob | null>(null);
  const speedDialRef = useRef<HTMLDivElement>(null);
  const canShare = !!(navigator.canShare && navigator.canShare({ files: [new File([''], 'test.jpg', { type: 'image/jpeg' })] }));
  const pageRef = useRef<HTMLDivElement>(null);
  const transformRef = useRef<ReactZoomPanPinchContentRef>(null);

  useEffect(() => {
    if (!code) return;

    getPhotoByCode(code)
      .then(result => {
        if (result) {
          setPhoto(result);
        } else {
          setError(t('photoNotFoundError'));
        }
      })
      .catch(err => setError(err instanceof Error ? err.message : t('photoNotFoundError')))
      .finally(() => setLoading(false));
  }, [code, t]);

  useEffect(() => {
    getAllPhotos()
      .then(photos => setAllCodes(photos.map(p => p.code)))
      .catch(() => {/* navigation won't work but photo display still will */});
  }, []);

  // Reset any lingering transform when code changes (component re-renders, not remounts)
  useEffect(() => {
    if (pageRef.current) {
      pageRef.current.style.transition = 'none';
      pageRef.current.style.transform = '';
    }
    transformRef.current?.resetTransform(0);
    // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional reset on navigation, component deliberately avoids remount
    setIsZoomed(false);
  }, [code]);

  // Reset speed dial state when navigating to a different photo
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- intentional reset on navigation, component deliberately avoids remount
    setPhotoAction('idle');
    cachedBlob.current = null;
  }, [code]);

  // Collapse speed dial when tapping outside it
  useEffect(() => {
    if (photoAction !== 'expanded') return;
    const handleClickOutside = (e: MouseEvent | TouchEvent) => {
      if (speedDialRef.current && !speedDialRef.current.contains(e.target as Node)) {
        setPhotoAction('idle');
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('touchstart', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('touchstart', handleClickOutside);
    };
  }, [photoAction]);

  const currentIndex = allCodes.indexOf(code ?? '');
  const prevCode = currentIndex > 0 ? allCodes[currentIndex - 1] : null;
  const nextCode = currentIndex >= 0 && currentIndex < allCodes.length - 1 ? allCodes[currentIndex + 1] : null;

  const goToPhoto = useCallback((targetCode: string) => {
    flushSync(() => {
      setPhoto(null);
      setLoading(true);
      setError(null);
    });
    navigate(`/${urlPrefix}/photo/${targetCode}`);
  }, [navigate, urlPrefix]);

  const handleSwipeLeft = useCallback(() => {
    if (nextCode) goToPhoto(nextCode);
  }, [nextCode, goToPhoto]);

  const handleSwipeRight = useCallback(() => {
    if (prevCode) goToPhoto(prevCode);
  }, [prevCode, goToPhoto]);

  useSwipeNavigation({ onSwipeLeft: handleSwipeLeft, onSwipeRight: handleSwipeRight, elementRef: pageRef, disabled: isZoomed });

  const handleToggle = async () => {
    if (photoAction === 'expanded') {
      setPhotoAction('idle');
      return;
    }
    if (cachedBlob.current) {
      setPhotoAction('expanded');
      return;
    }
    if (!photo) return;
    setPhotoAction('loading');
    try {
      const response = await fetch(getPhotoImageUrl(photo.id));
      if (!response.ok) throw new Error('Failed to fetch photo');
      cachedBlob.current = await response.blob();
      setPhotoAction('expanded');
    } catch {
      setPhotoAction('idle');
    }
  };

  const handleDownload = () => {
    if (!photo || !cachedBlob.current) return;
    const url = URL.createObjectURL(cachedBlob.current);
    const link = document.createElement('a');
    link.href = url;
    link.download = `photo-${photo.code}.jpg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  const handleShare = async () => {
    if (!photo || !cachedBlob.current) return;
    const file = new File([cachedBlob.current], `photo-${photo.code}.jpg`, { type: 'image/jpeg' });
    try {
      await navigator.share({ files: [file] });
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return;
    }
  };

  const handleBack = () => {
    navigate(`/${urlPrefix}/download`);
  };

  const navBar = (
    <div className="photo-detail-nav">
      <button onClick={handleBack} className="nav-back-button" aria-label={t('backToGallery')}>
        <ChevronLeftIcon size={28} />
      </button>
      <span className="photo-detail-code">{photo?.code ?? code}</span>
      <div className="nav-spacer" />
    </div>
  );

  if (loading) {
    return (
      <div className="photo-detail-page" ref={pageRef}>
        {navBar}
        <div className="photo-detail-loading">{t('loading')}</div>
      </div>
    );
  }

  if (error || !photo) {
    return (
      <div className="photo-detail-page" ref={pageRef}>
        {navBar}
        <div className="error-message">{error || t('photoNotFoundError')}</div>
      </div>
    );
  }

  return (
    <div className="photo-detail-page" ref={pageRef}>
      {navBar}
      <div className="photo-detail-content">
        {!isZoomed && prevCode && (
          <button
            onClick={() => goToPhoto(prevCode)}
            className="photo-detail-nav-arrow prev"
            aria-label={t('previousPhoto')}
          >
            <ChevronLeftIcon size={28} />
          </button>
        )}
        {!isZoomed && nextCode && (
          <button
            onClick={() => goToPhoto(nextCode)}
            className="photo-detail-nav-arrow next"
            aria-label={t('nextPhoto')}
          >
            <ChevronRightIcon size={28} />
          </button>
        )}
        <TransformWrapper
          ref={transformRef}
          minScale={1}
          maxScale={4}
          doubleClick={{ disabled: true }}
          wheel={{ step: 0.2 }}
          onTransform={(_ref, state) => setIsZoomed(state.scale > 1)}
        >
          <TransformComponent
            wrapperClass="photo-detail-zoom-wrapper"
            contentClass="photo-detail-zoom-content"
          >
            <img
              key={photo.id}
              src={getPhotoImageUrl(photo.id, 1200)}
              alt={`Photo ${photo.code}`}
              className="photo-detail-image"
            />
          </TransformComponent>
        </TransformWrapper>
      </div>
      <div className="photo-detail-actions">
        <div className={`speed-dial${photoAction === 'expanded' ? ' open' : ''}`} ref={speedDialRef}>
          <div className="speed-dial-items" aria-hidden={photoAction !== 'expanded'}>
            {canShare && (
              <button onClick={handleShare} className="action-button speed-dial-item" aria-label={t('sharePhoto')}>
                <ShareIcon size={24} />
              </button>
            )}
            <button onClick={handleDownload} className="action-button speed-dial-item" aria-label={t('downloadPhoto')}>
              <DownloadIcon size={24} />
            </button>
          </div>
          <button
            onClick={photoAction !== 'loading' ? handleToggle : undefined}
            className={`action-button speed-dial-trigger${photoAction === 'expanded' ? ' open' : ''}${photoAction === 'loading' ? ' loading' : ''}`}
            aria-label={t('getPhoto')}
            disabled={photoAction === 'loading'}
          >
            {photoAction === 'loading' ? <SpinnerIcon size={24} /> : <DotsVerticalIcon size={24} />}
          </button>
        </div>
      </div>
    </div>
  );
}

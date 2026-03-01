import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getPhotoByCode, getPhotoImageUrl, sharePhoto, getAllPhotos } from '../api/client';
import type { PhotoDto } from '../api/types';
import { ChevronLeftIcon, DownloadIcon, ShareIcon } from '../components/Icons';
import { useTranslation } from '../i18n/useTranslation';
import { useSwipeNavigation } from '../hooks/useSwipeNavigation';

export function PhotoDetailPage() {
  const { code } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [photo, setPhoto] = useState<PhotoDto | null>(null);
  const [loading, setLoading] = useState(!!code);
  const [error, setError] = useState<string | null>(code ? null : t('photoNotFoundError'));
  const [allCodes, setAllCodes] = useState<string[]>([]);
  const canShare = !!(navigator.canShare && navigator.canShare({ files: [new File([''], 'test.jpg', { type: 'image/jpeg' })] }));
  const pageRef = useRef<HTMLDivElement>(null);

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
  }, [code]);

  const currentIndex = allCodes.indexOf(code ?? '');
  const prevCode = currentIndex > 0 ? allCodes[currentIndex - 1] : null;
  const nextCode = currentIndex >= 0 && currentIndex < allCodes.length - 1 ? allCodes[currentIndex + 1] : null;

  const handleSwipeLeft = useCallback(() => {
    if (nextCode) navigate(`/photo/${nextCode}`);
  }, [nextCode, navigate]);

  const handleSwipeRight = useCallback(() => {
    if (prevCode) navigate(`/photo/${prevCode}`);
  }, [prevCode, navigate]);

  useSwipeNavigation({ onSwipeLeft: handleSwipeLeft, onSwipeRight: handleSwipeRight, elementRef: pageRef });

  const handleDownload = () => {
    if (!photo) return;

    const link = document.createElement('a');
    link.href = getPhotoImageUrl(photo.id);
    link.download = `photo-${photo.code}.jpg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleShare = async () => {
    if (!photo) return;
    await sharePhoto(photo.id, photo.code);
  };

  const handleBack = () => {
    navigate('/download');
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
        <img
          src={getPhotoImageUrl(photo.id, 1200)}
          alt={`Photo ${photo.code}`}
          className="photo-detail-image"
        />
      </div>
      <div className="photo-detail-actions">
        <button onClick={handleDownload} className="action-button" aria-label={t('downloadPhoto')}>
          <DownloadIcon size={24} />
        </button>
        {canShare && (
          <button onClick={handleShare} className="action-button" aria-label={t('sharePhoto')}>
            <ShareIcon size={24} />
          </button>
        )}
      </div>
    </div>
  );
}

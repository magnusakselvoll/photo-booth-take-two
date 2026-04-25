import { useState, useEffect, useLayoutEffect, useRef, useCallback } from 'react';
import { getPhotosPage, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';
import { useTranslation } from '../i18n/useTranslation';
import { getGalleryCache, setGalleryCache } from './galleryCache';

const PAGE_SIZE = 30;

interface PhotoGridProps {
  onPhotoClick: (code: string) => void;
}

export function PhotoGrid({ onPhotoClick }: PhotoGridProps) {
  const { t } = useTranslation();

  const cached = getGalleryCache();
  const hasCachedPhotos = cached.photos !== null && cached.photos.length > 0;
  const initialScrollTop = cached.scrollTop;

  const [photos, setPhotos] = useState<PhotoDto[]>(hasCachedPhotos ? cached.photos! : []);
  const [loading, setLoading] = useState(!hasCachedPhotos);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nextCursor, setNextCursor] = useState<string | null | undefined>(hasCachedPhotos ? cached.nextCursor : undefined);
  const sentinelRef = useRef<HTMLDivElement>(null);

  // Restore scroll position once after hydrating from cache, before paint
  useLayoutEffect(() => {
    if (hasCachedPhotos) {
      window.scrollTo(0, initialScrollTop);
    }
    // intentionally runs only once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Initial fetch, skipped when hydrating from cache
  useEffect(() => {
    if (hasCachedPhotos) return;
    getPhotosPage(PAGE_SIZE)
      .then(page => {
        setPhotos(page.photos);
        setNextCursor(page.nextCursor);
      })
      .catch(err => setError(err instanceof Error ? err.message : t('failedToLoadPhotos')))
      .finally(() => setLoading(false));
    // intentionally runs only once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Save to cache on unmount; re-registers whenever photos/nextCursor change so
  // the cleanup closure always captures the latest values
  useEffect(() => {
    return () => {
      setGalleryCache(photos, nextCursor, window.scrollY);
    };
  }, [photos, nextCursor]);

  const loadMore = useCallback(() => {
    if (!nextCursor || loadingMore) return;
    setLoadingMore(true);
    getPhotosPage(PAGE_SIZE, nextCursor)
      .then(page => {
        setPhotos(prev => [...prev, ...page.photos]);
        setNextCursor(page.nextCursor);
      })
      .catch(err => setError(err instanceof Error ? err.message : t('failedToLoadPhotos')))
      .finally(() => setLoadingMore(false));
  }, [nextCursor, loadingMore, t]);

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel) return;

    const observer = new IntersectionObserver(
      entries => {
        if (entries[0].isIntersecting) loadMore();
      },
      { rootMargin: '200px' }
    );

    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [loadMore]);

  if (loading) {
    return <div className="photo-grid-loading">{t('loadingPhotos')}</div>;
  }

  if (error) {
    return <div className="photo-grid-error">{error}</div>;
  }

  if (photos.length === 0) {
    return <div className="photo-grid-empty">{t('noPhotosYet')}</div>;
  }

  return (
    <div className="photo-grid">
      {photos.map(photo => (
        <div
          key={photo.id}
          className="photo-grid-item"
          onClick={() => onPhotoClick(photo.code)}
        >
          <img
            src={getPhotoImageUrl(photo.id, 400)}
            alt={`Photo ${photo.code}`}
            loading="lazy"
          />
        </div>
      ))}
      {nextCursor && (
        <div ref={sentinelRef} className="photo-grid-loading">
          {loadingMore ? t('loadingPhotos') : ''}
        </div>
      )}
    </div>
  );
}

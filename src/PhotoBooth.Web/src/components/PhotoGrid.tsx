import { useState, useEffect, useRef, useCallback } from 'react';
import { getPhotosPage, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';
import { useTranslation } from '../i18n/useTranslation';

const PAGE_SIZE = 30;

interface PhotoGridProps {
  onPhotoClick: (code: string) => void;
}

export function PhotoGrid({ onPhotoClick }: PhotoGridProps) {
  const { t } = useTranslation();
  const [photos, setPhotos] = useState<PhotoDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nextCursor, setNextCursor] = useState<string | null | undefined>(undefined);
  const sentinelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    getPhotosPage(PAGE_SIZE)
      .then(page => {
        setPhotos(page.photos);
        setNextCursor(page.nextCursor);
      })
      .catch(err => setError(err instanceof Error ? err.message : t('failedToLoadPhotos')))
      .finally(() => setLoading(false));
  }, [t]);

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

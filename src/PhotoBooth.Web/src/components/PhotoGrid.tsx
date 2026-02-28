import { useState, useEffect } from 'react';
import { getAllPhotos, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';
import { useTranslation } from '../i18n/useTranslation';

interface PhotoGridProps {
  onPhotoClick: (code: string) => void;
}

export function PhotoGrid({ onPhotoClick }: PhotoGridProps) {
  const { t } = useTranslation();
  const [photos, setPhotos] = useState<PhotoDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAllPhotos()
      .then(setPhotos)
      .catch(err => setError(err instanceof Error ? err.message : t('failedToLoadPhotos')))
      .finally(() => setLoading(false));
  }, [t]);

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
          <div className="photo-grid-code">{photo.code}</div>
        </div>
      ))}
    </div>
  );
}

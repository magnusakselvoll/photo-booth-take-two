import { useState, useEffect } from 'react';
import { getAllPhotos, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';

interface PhotoGridProps {
  onPhotoClick: (code: string) => void;
}

export function PhotoGrid({ onPhotoClick }: PhotoGridProps) {
  const [photos, setPhotos] = useState<PhotoDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAllPhotos()
      .then(setPhotos)
      .catch(err => setError(err instanceof Error ? err.message : 'Failed to load photos'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <div className="photo-grid-loading">Loading photos...</div>;
  }

  if (error) {
    return <div className="photo-grid-error">{error}</div>;
  }

  if (photos.length === 0) {
    return <div className="photo-grid-empty">No photos yet</div>;
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
            src={getPhotoImageUrl(photo.id)}
            alt={`Photo ${photo.code}`}
            loading="lazy"
          />
          <div className="photo-grid-code">{photo.code}</div>
        </div>
      ))}
    </div>
  );
}

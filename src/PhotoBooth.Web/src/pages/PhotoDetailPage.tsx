import { useState, useEffect } from 'react';
import { getPhotoByCode, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';

interface PhotoDetailPageProps {
  code: string;
}

export function PhotoDetailPage({ code }: PhotoDetailPageProps) {
  const [photo, setPhoto] = useState<PhotoDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getPhotoByCode(code)
      .then(result => {
        if (result) {
          setPhoto(result);
        } else {
          setError('Photo not found');
        }
      })
      .catch(err => setError(err instanceof Error ? err.message : 'Failed to load photo'))
      .finally(() => setLoading(false));
  }, [code]);

  const handleDownload = () => {
    if (!photo) return;

    const link = document.createElement('a');
    link.href = getPhotoImageUrl(photo.id);
    link.download = `photo-${photo.code}.jpg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleBack = () => {
    window.location.hash = '#download';
  };

  if (loading) {
    return (
      <div className="photo-detail-page">
        <div className="photo-detail-loading">Loading...</div>
      </div>
    );
  }

  if (error || !photo) {
    return (
      <div className="photo-detail-page">
        <div className="error-message">{error || 'Photo not found'}</div>
        <button onClick={handleBack} className="back-button">
          Back to Gallery
        </button>
      </div>
    );
  }

  return (
    <div className="photo-detail-page">
      <div className="photo-detail-header">
        <button onClick={handleBack} className="back-button">
          Back to Gallery
        </button>
        <span className="photo-detail-code">Code: {photo.code}</span>
      </div>
      <div className="photo-detail-content">
        <img
          src={getPhotoImageUrl(photo.id)}
          alt={`Photo ${photo.code}`}
          className="photo-detail-image"
        />
        <button onClick={handleDownload} className="download-button">
          Download Photo
        </button>
      </div>
    </div>
  );
}

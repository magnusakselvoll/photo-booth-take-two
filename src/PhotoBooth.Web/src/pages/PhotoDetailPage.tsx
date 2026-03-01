import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getPhotoByCode, getPhotoImageUrl, sharePhoto } from '../api/client';
import type { PhotoDto } from '../api/types';
import { useTranslation } from '../i18n/useTranslation';

export function PhotoDetailPage() {
  const { code } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [photo, setPhoto] = useState<PhotoDto | null>(null);
  const [loading, setLoading] = useState(!!code);
  const [error, setError] = useState<string | null>(code ? null : t('photoNotFoundError'));
  const [canShare, setCanShare] = useState(false);

  useEffect(() => {
    if (navigator.canShare) {
      const testFile = new File([''], 'test.jpg', { type: 'image/jpeg' });
      setCanShare(navigator.canShare({ files: [testFile] }));
    }
  }, []);

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

  if (loading) {
    return (
      <div className="photo-detail-page">
        <div className="photo-detail-loading">{t('loading')}</div>
      </div>
    );
  }

  if (error || !photo) {
    return (
      <div className="photo-detail-page">
        <div className="error-message">{error || t('photoNotFoundError')}</div>
        <button onClick={handleBack} className="back-button">
          {t('backToGallery')}
        </button>
      </div>
    );
  }

  return (
    <div className="photo-detail-page">
      <div className="photo-detail-header">
        <button onClick={handleBack} className="back-button">
          {t('backToGallery')}
        </button>
        <span className="photo-detail-code">{t('code')}: {photo.code}</span>
      </div>
      <div className="photo-detail-content">
        <img
          src={getPhotoImageUrl(photo.id, 1200)}
          alt={`Photo ${photo.code}`}
          className="photo-detail-image"
        />
        <div className="photo-result-actions">
          <button onClick={handleDownload} className="download-button">
            {t('downloadPhoto')}
          </button>
          {canShare && (
            <button onClick={handleShare} className="share-button">
              {t('sharePhoto')}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

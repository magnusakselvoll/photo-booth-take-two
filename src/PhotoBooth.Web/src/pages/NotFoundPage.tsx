import { useNavigate } from 'react-router-dom';
import { useTranslation } from '../i18n/useTranslation';

export function NotFoundPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div className="not-found-page">
      <span className="not-found-code">404</span>
      <p className="not-found-message">{t('pageNotFound')}</p>
      <button className="not-found-link" onClick={() => navigate('/download')}>
        {t('goToGallery')}
      </button>
    </div>
  );
}

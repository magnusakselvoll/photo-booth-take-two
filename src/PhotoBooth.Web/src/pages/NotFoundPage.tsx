import { useTranslation } from '../i18n/useTranslation';

export function NotFoundPage() {
  const { t } = useTranslation();

  return (
    <div className="not-found-page">
      <span className="not-found-code">404</span>
      <p className="not-found-message">{t('pageNotFound')}</p>
    </div>
  );
}

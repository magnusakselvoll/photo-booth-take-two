import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PhotoGrid } from '../components/PhotoGrid';
import { SearchIcon } from '../components/Icons';
import { useTranslation } from '../i18n/useTranslation';
import { hasGalleryCache } from '../components/galleryCache';

interface DownloadPageProps {
  urlPrefix: string;
}

export function DownloadPage({ urlPrefix }: DownloadPageProps) {
  const navigate = useNavigate();
  const { t, language, setLanguage } = useTranslation();
  const [code, setCode] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = code.trim();
    if (trimmed) {
      navigate(`/${urlPrefix}/photo/${trimmed}`);
    }
  };

  const handlePhotoClick = (photoCode: string) => {
    navigate(`/${urlPrefix}/photo/${photoCode}`);
  };

  return (
    <div className="download-page">
      <form onSubmit={handleSubmit} className="search-bar">
        <input
          type="text"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder={t('enterPhotoCode')}
          className="code-input"
          maxLength={10}
          inputMode="numeric"
          autoFocus={!hasGalleryCache()}
        />
        <button type="submit" disabled={!code.trim()} className="search-button" aria-label={t('findPhoto')}>
          <SearchIcon size={20} />
        </button>
      </form>

      <PhotoGrid onPhotoClick={handlePhotoClick} />

      <footer className="language-footer">
        <button
          onClick={() => setLanguage('en')}
          className={`language-button ${language === 'en' ? 'active' : ''}`}
        >
          English
        </button>
        <span className="language-separator">|</span>
        <button
          onClick={() => setLanguage('es')}
          className={`language-button ${language === 'es' ? 'active' : ''}`}
        >
          Espa&ntilde;ol
        </button>
      </footer>
    </div>
  );
}

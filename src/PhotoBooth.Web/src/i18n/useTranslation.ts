import { useState, useEffect, useMemo, useCallback } from 'react';
import { translations, type Language, type TranslationKey } from './translations';

function getLanguageFromUrl(): Language | null {
  const params = new URLSearchParams(window.location.search);
  const lang = params.get('lang');
  if (lang === 'es' || lang === 'en') return lang;
  return null;
}

function getDeviceLanguage(): Language {
  const browserLang = navigator.language.split('-')[0];
  if (browserLang === 'es') return 'es';
  return 'en';
}

function getInitialLanguage(): Language {
  return getLanguageFromUrl() ?? getDeviceLanguage();
}

export function useTranslation() {
  const [language, setLanguageState] = useState<Language>(getInitialLanguage);
  const t = useMemo(() => translations[language], [language]);

  useEffect(() => {
    const handlePopState = () => {
      const urlLang = getLanguageFromUrl();
      if (urlLang) {
        setLanguageState(urlLang);
      }
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  const setLanguage = useCallback((newLang: Language) => {
    setLanguageState(newLang);
    const url = new URL(window.location.href);
    url.searchParams.set('lang', newLang);
    window.history.replaceState({}, '', url.toString());
  }, []);

  return {
    t: (key: TranslationKey) => t[key],
    language,
    setLanguage,
  };
}

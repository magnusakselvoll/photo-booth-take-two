import { useMemo } from 'react';
import { translations, type Language, type TranslationKey } from './translations';

function getDeviceLanguage(): Language {
  const browserLang = navigator.language.split('-')[0];
  if (browserLang === 'es') return 'es';
  return 'en';
}

export function useTranslation() {
  const language = useMemo(() => getDeviceLanguage(), []);
  const t = useMemo(() => translations[language], [language]);

  return {
    t: (key: TranslationKey) => t[key],
    language,
  };
}

import { useState, useEffect, useRef } from 'react';
import { PhotoDisplay } from './PhotoDisplay';
import type { KenBurnsConfig } from './PhotoDisplay';
import { useTranslation } from '../i18n/useTranslation';
import type { SlideshowPhoto } from '../hooks/useSlideshowNavigation';
import { generateKenBurnsConfig } from '../utils/kenBurns';

interface SlideshowProps {
  photo: SlideshowPhoto | null;
  qrCodeBaseUrl?: string;
  urlPrefix?: string;
  swirlEffect?: boolean;
  slideshowIntervalMs?: number;
}

const FADE_DURATION_MS = 500;

interface PhotoState {
  photo: SlideshowPhoto;
  kenBurns: KenBurnsConfig;
  key: number;
}

export function Slideshow({ photo, qrCodeBaseUrl, urlPrefix = '', swirlEffect = true, slideshowIntervalMs = 30000 }: SlideshowProps) {
  const { t } = useTranslation();
  const [currentState, setCurrentState] = useState<PhotoState | null>(null);
  const [previousState, setPreviousState] = useState<PhotoState | null>(null);
  const photoKeyRef = useRef(0);
  const lastPhotoIdRef = useRef<string | null>(null);

  // Update display when photo changes
  useEffect(() => {
    if (!photo) {
      lastPhotoIdRef.current = null;
      return;
    }

    // Only update if photo ID changed
    if (photo.id === lastPhotoIdRef.current) {
      return;
    }

    lastPhotoIdRef.current = photo.id;

    setCurrentState((prev) => {
      // Move current to previous for crossfade
      if (prev) {
        setPreviousState(prev);
        setTimeout(() => setPreviousState(null), FADE_DURATION_MS);
      }
      photoKeyRef.current += 1;
      return {
        photo,
        kenBurns: generateKenBurnsConfig(slideshowIntervalMs),
        key: photoKeyRef.current,
      };
    });
  }, [photo, slideshowIntervalMs]);

  if (!photo || !currentState) {
    return <div className="slideshow-empty">{t('noPhotosToShow')}</div>;
  }

  return (
    <div className="slideshow">
      {previousState && (
        <PhotoDisplay
          key={previousState.key}
          photoId={previousState.photo.id}
          code={previousState.photo.code}
          kenBurns={previousState.kenBurns}
          qrCodeBaseUrl={qrCodeBaseUrl}
          urlPrefix={urlPrefix}
          swirlEffect={swirlEffect}
          fadingOut
        />
      )}
      <PhotoDisplay
        key={currentState.key}
        photoId={currentState.photo.id}
        code={currentState.photo.code}
        kenBurns={currentState.kenBurns}
        qrCodeBaseUrl={qrCodeBaseUrl}
        urlPrefix={urlPrefix}
        swirlEffect={swirlEffect}
      />
    </div>
  );
}

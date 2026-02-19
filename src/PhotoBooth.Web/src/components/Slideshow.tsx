import { useState, useEffect, useRef } from 'react';
import { PhotoDisplay } from './PhotoDisplay';
import type { KenBurnsConfig } from './PhotoDisplay';
import { useTranslation } from '../i18n/useTranslation';
import type { SlideshowPhoto } from '../hooks/useSlideshowNavigation';

interface SlideshowProps {
  photo: SlideshowPhoto | null;
  qrCodeBaseUrl?: string;
  swirlEffect?: boolean;
  slideshowIntervalMs?: number;
}

const FADE_DURATION_MS = 500;

function randomInRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

function generateKenBurnsConfig(intervalMs: number): KenBurnsConfig {
  // Random zoom direction: in or out
  const zoomIn = Math.random() > 0.5;

  // Stronger scale range: 1.08-1.12 to 1.18-1.28
  const scaleSmall = randomInRange(1.08, 1.12);
  const scaleLarge = randomInRange(1.18, 1.28);

  // Random pan direction with randomized amount (3-6%)
  const panAmount = randomInRange(3, 6);
  const panDirections = [
    { x: panAmount, y: 0 },      // right
    { x: -panAmount, y: 0 },     // left
    { x: 0, y: panAmount },      // down
    { x: 0, y: -panAmount },     // up
    { x: panAmount * 0.7, y: panAmount * 0.7 },   // diagonal down-right
    { x: -panAmount * 0.7, y: panAmount * 0.7 },  // diagonal down-left
    { x: panAmount * 0.7, y: -panAmount * 0.7 },  // diagonal up-right
    { x: -panAmount * 0.7, y: -panAmount * 0.7 }, // diagonal up-left
  ];
  const pan = panDirections[Math.floor(Math.random() * panDirections.length)];

  // Duration slightly overshoots the slideshow interval to avoid freezing at the end
  const duration = intervalMs / 1000;

  if (zoomIn) {
    return {
      scaleFrom: scaleSmall,
      scaleTo: scaleLarge,
      xFrom: '0%',
      yFrom: '0%',
      xTo: `${pan.x}%`,
      yTo: `${pan.y}%`,
      duration: `${duration.toFixed(1)}s`,
    };
  } else {
    return {
      scaleFrom: scaleLarge,
      scaleTo: scaleSmall,
      xFrom: '0%',
      yFrom: '0%',
      xTo: `${pan.x}%`,
      yTo: `${pan.y}%`,
      duration: `${duration.toFixed(1)}s`,
    };
  }
}

interface PhotoState {
  photo: SlideshowPhoto;
  kenBurns: KenBurnsConfig;
  key: number;
}

export function Slideshow({ photo, qrCodeBaseUrl, swirlEffect = true, slideshowIntervalMs = 30000 }: SlideshowProps) {
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
  }, [photo]);

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
        swirlEffect={swirlEffect}
      />
    </div>
  );
}

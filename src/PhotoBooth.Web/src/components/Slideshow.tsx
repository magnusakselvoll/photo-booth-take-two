import { useState, useEffect, useCallback, useRef } from 'react';
import { getNextSlideshowPhoto } from '../api/client';
import type { SlideshowPhotoDto } from '../api/types';
import { PhotoDisplay } from './PhotoDisplay';
import type { KenBurnsConfig } from './PhotoDisplay';
import { useTranslation } from '../i18n/useTranslation';

interface SlideshowProps {
  intervalMs?: number;
  paused?: boolean;
  qrCodeBaseUrl?: string;
}

function randomInRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

function generateKenBurnsConfig(): KenBurnsConfig {
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

  // Randomized duration (8-10 seconds) - must cover slideshow interval
  const duration = randomInRange(8, 10);

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
  photo: SlideshowPhotoDto;
  kenBurns: KenBurnsConfig;
  key: number;
}

const FADE_DURATION_MS = 500;

export function Slideshow({ intervalMs = 8000, paused = false, qrCodeBaseUrl }: SlideshowProps) {
  const { t } = useTranslation();
  const [currentState, setCurrentState] = useState<PhotoState | null>(null);
  const [previousState, setPreviousState] = useState<PhotoState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const photoKeyRef = useRef(0);

  const loadNextPhoto = useCallback(async () => {
    try {
      const photo = await getNextSlideshowPhoto();
      if (photo) {
        setCurrentState((prev) => {
          // Move current to previous for crossfade
          if (prev) {
            setPreviousState(prev);
            // Clear previous after fade completes
            setTimeout(() => setPreviousState(null), FADE_DURATION_MS);
          }
          photoKeyRef.current += 1;
          return {
            photo,
            kenBurns: generateKenBurnsConfig(),
            key: photoKeyRef.current,
          };
        });
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load photo');
    }
  }, []);

  useEffect(() => {
    loadNextPhoto();
  }, [loadNextPhoto]);

  useEffect(() => {
    if (paused) return;

    const interval = setInterval(loadNextPhoto, intervalMs);
    return () => clearInterval(interval);
  }, [intervalMs, paused, loadNextPhoto]);

  if (error) {
    return <div className="slideshow-error">{error}</div>;
  }

  if (!currentState) {
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
          fadingOut
        />
      )}
      <PhotoDisplay
        key={currentState.key}
        photoId={currentState.photo.id}
        code={currentState.photo.code}
        kenBurns={currentState.kenBurns}
        qrCodeBaseUrl={qrCodeBaseUrl}
      />
    </div>
  );
}

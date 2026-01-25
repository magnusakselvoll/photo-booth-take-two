import { useState, useEffect, useCallback } from 'react';
import { getNextSlideshowPhoto } from '../api/client';
import type { SlideshowPhotoDto } from '../api/types';
import { PhotoDisplay } from './PhotoDisplay';

interface SlideshowProps {
  intervalMs?: number;
  paused?: boolean;
}

export function Slideshow({ intervalMs = 8000, paused = false }: SlideshowProps) {
  const [currentPhoto, setCurrentPhoto] = useState<SlideshowPhotoDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadNextPhoto = useCallback(async () => {
    try {
      const photo = await getNextSlideshowPhoto();
      if (photo) {
        setCurrentPhoto(photo);
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

  if (!currentPhoto) {
    return <div className="slideshow-empty">No photos yet</div>;
  }

  return (
    <div className="slideshow">
      <PhotoDisplay photoId={currentPhoto.id} code={currentPhoto.code} />
    </div>
  );
}

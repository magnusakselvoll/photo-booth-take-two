import { useState, useEffect, useCallback, useRef } from 'react';
import { getAllPhotos } from '../api/client';
import type { PhotoDto } from '../api/types';

export interface SlideshowPhoto {
  id: string;
  code: string;
}

interface UseSlideshowNavigationOptions {
  intervalMs?: number;
  paused?: boolean;
  initialRandom?: boolean;
}

interface UseSlideshowNavigationResult {
  currentPhoto: SlideshowPhoto | null;
  isRandom: boolean;
  currentIndex: number;
  totalPhotos: number;
  goNext: () => void;
  goPrevious: () => void;
  skip: (n: number) => void;
  toggleMode: () => void;
  refresh: () => Promise<void>;
}

function shuffleArray<T>(array: T[]): T[] {
  const result = [...array];
  for (let i = result.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}

export function useSlideshowNavigation({
  intervalMs = 8000,
  paused = false,
  initialRandom = true,
}: UseSlideshowNavigationOptions = {}): UseSlideshowNavigationResult {
  const [photos, setPhotos] = useState<PhotoDto[]>([]);
  const [shuffledIndices, setShuffledIndices] = useState<number[]>([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isRandom, setIsRandom] = useState(initialRandom);
  const intervalRef = useRef<number | null>(null);

  const generateShuffledIndices = useCallback((length: number) => {
    const indices = Array.from({ length }, (_, i) => i);
    return shuffleArray(indices);
  }, []);

  const loadPhotos = useCallback(async () => {
    try {
      const allPhotos = await getAllPhotos();
      setPhotos(allPhotos);
      if (allPhotos.length > 0) {
        const newShuffled = generateShuffledIndices(allPhotos.length);
        setShuffledIndices(newShuffled);
        setCurrentIndex(0);
      }
    } catch (err) {
      console.error('Failed to load photos:', err);
    }
  }, [generateShuffledIndices]);

  useEffect(() => {
    loadPhotos();
  }, [loadPhotos]);

  const getEffectiveIndex = useCallback((index: number): number => {
    if (photos.length === 0) return 0;
    if (isRandom) {
      return shuffledIndices[index] ?? 0;
    }
    return index;
  }, [photos.length, isRandom, shuffledIndices]);

  const goNext = useCallback(() => {
    if (photos.length === 0) return;
    setCurrentIndex(prev => (prev + 1) % photos.length);
  }, [photos.length]);

  const goPrevious = useCallback(() => {
    if (photos.length === 0) return;
    setCurrentIndex(prev => (prev - 1 + photos.length) % photos.length);
  }, [photos.length]);

  const skip = useCallback((n: number) => {
    if (photos.length === 0) return;
    setCurrentIndex(prev => {
      const newIndex = prev + n;
      // Wrap around
      return ((newIndex % photos.length) + photos.length) % photos.length;
    });
  }, [photos.length]);

  const toggleMode = useCallback(() => {
    if (photos.length === 0) return;

    setIsRandom(prev => {
      const newIsRandom = !prev;

      // Get the current photo's actual index in the photos array
      const currentPhotoIndex = prev
        ? shuffledIndices[currentIndex]
        : currentIndex;

      if (newIsRandom) {
        // Switching to random mode - generate new shuffle and find current photo
        const newShuffled = generateShuffledIndices(photos.length);
        setShuffledIndices(newShuffled);
        // Find where the current photo ended up in the shuffle
        const newPosition = newShuffled.indexOf(currentPhotoIndex);
        setCurrentIndex(newPosition >= 0 ? newPosition : 0);
      } else {
        // Switching to sorted mode - use the actual photo index
        setCurrentIndex(currentPhotoIndex);
      }

      return newIsRandom;
    });
  }, [photos.length, currentIndex, shuffledIndices, generateShuffledIndices]);

  const refresh = useCallback(async () => {
    // Save current photo info to try to stay on the same photo
    const currentPhotoIndex = isRandom
      ? shuffledIndices[currentIndex]
      : currentIndex;
    const currentPhotoId = photos[currentPhotoIndex]?.id;

    try {
      const allPhotos = await getAllPhotos();
      setPhotos(allPhotos);

      if (allPhotos.length > 0) {
        const newShuffled = generateShuffledIndices(allPhotos.length);
        setShuffledIndices(newShuffled);

        // Try to find the current photo in the new list
        if (currentPhotoId) {
          const photoIndexInNew = allPhotos.findIndex(p => p.id === currentPhotoId);
          if (photoIndexInNew >= 0) {
            if (isRandom) {
              const posInShuffle = newShuffled.indexOf(photoIndexInNew);
              setCurrentIndex(posInShuffle >= 0 ? posInShuffle : 0);
            } else {
              setCurrentIndex(photoIndexInNew);
            }
            return;
          }
        }
        // Photo not found or no current photo, reset to start
        setCurrentIndex(0);
      }
    } catch (err) {
      console.error('Failed to refresh photos:', err);
    }
  }, [isRandom, shuffledIndices, currentIndex, photos, generateShuffledIndices]);

  // Auto-advance timer
  useEffect(() => {
    if (paused || photos.length === 0) {
      if (intervalRef.current !== null) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
      return;
    }

    intervalRef.current = window.setInterval(goNext, intervalMs);
    return () => {
      if (intervalRef.current !== null) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [paused, photos.length, intervalMs, goNext]);

  const effectivePhotoIndex = getEffectiveIndex(currentIndex);
  const currentPhoto = photos.length > 0 && photos[effectivePhotoIndex]
    ? { id: photos[effectivePhotoIndex].id, code: photos[effectivePhotoIndex].code }
    : null;

  return {
    currentPhoto,
    isRandom,
    currentIndex,
    totalPhotos: photos.length,
    goNext,
    goPrevious,
    skip,
    toggleMode,
    refresh,
  };
}

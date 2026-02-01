import { useState, useCallback, useEffect, useRef } from 'react';
import { Slideshow } from '../components/Slideshow';
import { CaptureOverlay } from '../components/CaptureOverlay';
import { PhotoDisplay, type KenBurnsConfig } from '../components/PhotoDisplay';
import { useEventStream } from '../api/events';
import { triggerCapture } from '../api/client';
import type { PhotoBoothEvent, QueuedPhoto } from '../api/types';

const PREVIEW_DURATION_MS = 8000; // Same as slideshow interval
const ERROR_DISPLAY_MS = 3000;
const FADE_DURATION_MS = 500;
const WATCHDOG_RELOAD_MS = 5 * 60 * 1000;

interface BoothPageProps {
  qrCodeBaseUrl?: string;
}

function randomInRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

function generateKenBurnsConfig(): KenBurnsConfig {
  const zoomIn = Math.random() > 0.5;
  const scaleSmall = randomInRange(1.08, 1.12);
  const scaleLarge = randomInRange(1.18, 1.28);
  const panAmount = randomInRange(3, 6);
  const panDirections = [
    { x: panAmount, y: 0 },
    { x: -panAmount, y: 0 },
    { x: 0, y: panAmount },
    { x: 0, y: -panAmount },
    { x: panAmount * 0.7, y: panAmount * 0.7 },
    { x: -panAmount * 0.7, y: panAmount * 0.7 },
    { x: panAmount * 0.7, y: -panAmount * 0.7 },
    { x: -panAmount * 0.7, y: -panAmount * 0.7 },
  ];
  const pan = panDirections[Math.floor(Math.random() * panDirections.length)];
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

interface DisplayPhoto {
  photo: QueuedPhoto;
  kenBurns: KenBurnsConfig;
  key: number;
  fromQueue: boolean; // true if from queue, false if newly captured
}

export function BoothPage({ qrCodeBaseUrl }: BoothPageProps) {
  // Queue of interrupted photos waiting to be displayed (FIFO)
  const [photoQueue, setPhotoQueue] = useState<QueuedPhoto[]>([]);
  // Currently displaying photo
  const [currentDisplay, setCurrentDisplay] = useState<DisplayPhoto | null>(null);
  // Previous photo for crossfade
  const [previousDisplay, setPreviousDisplay] = useState<DisplayPhoto | null>(null);
  // Number of active countdowns
  const [activeCountdowns, setActiveCountdowns] = useState(0);
  // Error message
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  // Countdown duration from latest event
  const [countdownDurationMs, setCountdownDurationMs] = useState(3000);

  const watchdogTimeoutRef = useRef<number | null>(null);
  const previewTimeoutRef = useRef<number | null>(null);
  const errorTimeoutRef = useRef<number | null>(null);
  const photoKeyRef = useRef(0);
  // Track the current display for use in callbacks (avoid stale closure)
  const currentDisplayRef = useRef<DisplayPhoto | null>(null);

  const resetWatchdog = useCallback(() => {
    if (watchdogTimeoutRef.current !== null) {
      clearTimeout(watchdogTimeoutRef.current);
    }
    watchdogTimeoutRef.current = window.setTimeout(() => {
      console.log('Watchdog: No interaction for 5 minutes, reloading page...');
      window.location.reload();
    }, WATCHDOG_RELOAD_MS);
  }, []);

  useEffect(() => {
    resetWatchdog();
    return () => {
      if (watchdogTimeoutRef.current !== null) {
        clearTimeout(watchdogTimeoutRef.current);
      }
    };
  }, [resetWatchdog]);

  // Keep ref in sync with state
  useEffect(() => {
    currentDisplayRef.current = currentDisplay;
  }, [currentDisplay]);

  // Helper to start showing a photo
  const startShowingPhoto = useCallback((photo: QueuedPhoto, fromQueue: boolean) => {
    console.log('Starting to show photo:', photo.code, fromQueue ? '(from queue)' : '(newly captured)');

    // Move current to previous for crossfade
    setCurrentDisplay(prev => {
      if (prev) {
        setPreviousDisplay(prev);
        setTimeout(() => setPreviousDisplay(null), FADE_DURATION_MS);
      }
      return prev;
    });

    photoKeyRef.current += 1;
    const newDisplay: DisplayPhoto = {
      photo,
      kenBurns: generateKenBurnsConfig(),
      key: photoKeyRef.current,
      fromQueue,
    };
    setCurrentDisplay(newDisplay);

    // Schedule completion
    if (previewTimeoutRef.current !== null) {
      clearTimeout(previewTimeoutRef.current);
    }
    previewTimeoutRef.current = window.setTimeout(() => {
      console.log('Preview complete for:', photo.code);

      // If it was from queue, remove it (it was shown fully)
      if (fromQueue) {
        setPhotoQueue(q => q.filter(p => p.photoId !== photo.photoId));
      }

      // Clear display to trigger showing next from queue or slideshow
      setCurrentDisplay(null);
    }, PREVIEW_DURATION_MS);
  }, []);

  // Show next photo from queue
  const showNextFromQueue = useCallback(() => {
    setPhotoQueue(queue => {
      if (queue.length === 0) {
        return queue;
      }

      const [nextPhoto] = queue;
      // Don't remove from queue yet - will be removed when preview completes
      startShowingPhoto(nextPhoto, true);
      return queue;
    });
  }, [startShowingPhoto]);

  // Process queue when conditions change
  useEffect(() => {
    // Start showing queue when no countdowns and nothing currently displayed
    if (activeCountdowns === 0 && currentDisplay === null && photoQueue.length > 0) {
      showNextFromQueue();
    }
  }, [activeCountdowns, currentDisplay, photoQueue.length, showNextFromQueue]);

  // Handle interruption - adds current photo to queue if it was newly captured
  const handleInterruption = useCallback(() => {
    const display = currentDisplayRef.current;
    if (display === null) return;

    console.log('Interrupting photo:', display.photo.code, display.fromQueue ? '(from queue - stays)' : '(new - adding to queue)');

    // Cancel completion timer
    if (previewTimeoutRef.current !== null) {
      clearTimeout(previewTimeoutRef.current);
      previewTimeoutRef.current = null;
    }

    // If it was newly captured (not from queue), add to end of queue
    if (!display.fromQueue) {
      setPhotoQueue(queue => [...queue, display.photo]);
    }
    // If from queue, it stays in queue (we never removed it)

    // Clear display
    setCurrentDisplay(null);
  }, []);

  const handleEvent = useCallback((event: PhotoBoothEvent) => {
    switch (event.eventType) {
      case 'countdown-started':
        console.log('Countdown started, duration:', event.durationMs);
        setCountdownDurationMs(event.durationMs);
        setActiveCountdowns(count => count + 1);

        // Interrupt current display if any
        handleInterruption();
        break;

      case 'photo-captured': {
        console.log('Photo captured:', event.code);
        setActiveCountdowns(count => Math.max(0, count - 1));

        const newPhoto: QueuedPhoto = {
          photoId: event.photoId,
          code: event.code,
          imageUrl: event.imageUrl,
          timestamp: event.timestamp,
        };

        // Show immediately (not added to queue - only goes to queue if interrupted)
        startShowingPhoto(newPhoto, false);
        break;
      }

      case 'capture-failed':
        console.error('Capture failed:', event.error);
        setActiveCountdowns(count => Math.max(0, count - 1));
        setErrorMessage(event.error);
        if (errorTimeoutRef.current !== null) {
          clearTimeout(errorTimeoutRef.current);
        }
        errorTimeoutRef.current = window.setTimeout(() => {
          setErrorMessage(null);
        }, ERROR_DISPLAY_MS);
        break;
    }
  }, [handleInterruption, startShowingPhoto]);

  useEventStream(handleEvent);

  const handleTrigger = async () => {
    resetWatchdog();
    console.log('Click detected, triggering capture...');

    try {
      await triggerCapture();
    } catch (err) {
      console.error('Trigger error:', err);
      setErrorMessage(err instanceof Error ? err.message : 'Failed to trigger capture');
      if (errorTimeoutRef.current !== null) {
        clearTimeout(errorTimeoutRef.current);
      }
      errorTimeoutRef.current = window.setTimeout(() => {
        setErrorMessage(null);
      }, ERROR_DISPLAY_MS);
    }
  };

  const handleCountdownComplete = () => {
    // Server handles actual capture
  };

  // Determine what to show
  const showCapturedPhoto = currentDisplay !== null;
  const showSlideshow = !showCapturedPhoto;
  const showCountdown = activeCountdowns > 0;
  const showError = errorMessage !== null;

  return (
    <div className="booth-page" onClick={handleTrigger}>
      {/* Show slideshow when not showing captured photos */}
      {showSlideshow && <Slideshow paused={showCountdown} qrCodeBaseUrl={qrCodeBaseUrl} />}

      {/* Show captured photo with same Ken Burns effect as slideshow */}
      {showCapturedPhoto && (
        <div className="slideshow">
          {previousDisplay && (
            <PhotoDisplay
              key={previousDisplay.key}
              photoId={previousDisplay.photo.photoId}
              code={previousDisplay.photo.code}
              kenBurns={previousDisplay.kenBurns}
              qrCodeBaseUrl={qrCodeBaseUrl}
              fadingOut
            />
          )}
          <PhotoDisplay
            key={currentDisplay.key}
            photoId={currentDisplay.photo.photoId}
            code={currentDisplay.photo.code}
            kenBurns={currentDisplay.kenBurns}
            qrCodeBaseUrl={qrCodeBaseUrl}
          />
        </div>
      )}

      {/* Countdown overlay */}
      {showCountdown && (
        <CaptureOverlay durationMs={countdownDurationMs} onComplete={handleCountdownComplete} />
      )}

      {/* Error overlay */}
      {showError && (
        <div className="error-display">
          <div className="error-message">{errorMessage}</div>
        </div>
      )}
    </div>
  );
}

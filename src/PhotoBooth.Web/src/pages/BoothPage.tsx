import { useState, useCallback, useEffect, useRef } from 'react';
import { Slideshow } from '../components/Slideshow';
import { CaptureOverlay } from '../components/CaptureOverlay';
import { PhotoDisplay, type KenBurnsConfig } from '../components/PhotoDisplay';
import { useEventStream } from '../api/events';
import { triggerCapture } from '../api/client';
import { useSlideshowNavigation } from '../hooks/useSlideshowNavigation';
import { useKeyboardNavigation } from '../hooks/useKeyboardNavigation';
import { useGamepadNavigation } from '../hooks/useGamepadNavigation';
import type { GamepadDebugEvent } from '../hooks/useGamepadNavigation';
import type { PhotoBoothEvent, QueuedPhoto, GamepadConfig } from '../api/types';

const DEFAULT_SLIDESHOW_INTERVAL_MS = 30000;
const ERROR_DISPLAY_MS = 3000;
const FADE_DURATION_MS = 500;
const WATCHDOG_RELOAD_MS = 5 * 60 * 1000;
const GAMEPAD_DEBUG_DISPLAY_MS = 3000;

interface BoothPageProps {
  qrCodeBaseUrl?: string;
  swirlEffect?: boolean;
  slideshowIntervalMs?: number;
  gamepadConfig?: GamepadConfig | null;
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

export function BoothPage({ qrCodeBaseUrl, swirlEffect = true, slideshowIntervalMs = DEFAULT_SLIDESHOW_INTERVAL_MS, gamepadConfig }: BoothPageProps) {
  // Queue of interrupted photos waiting to be displayed
  const [photoQueue, setPhotoQueue] = useState<QueuedPhoto[]>([]);
  // Current index within the queue
  const [queueIndex, setQueueIndex] = useState(0);
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

  const [gamepadDebugEvent, setGamepadDebugEvent] = useState<GamepadDebugEvent | null>(null);

  const watchdogTimeoutRef = useRef<number | null>(null);
  const previewTimeoutRef = useRef<number | null>(null);
  const errorTimeoutRef = useRef<number | null>(null);
  const gamepadDebugTimeoutRef = useRef<number | null>(null);
  const photoKeyRef = useRef(0);
  // Track the current display for use in callbacks (avoid stale closure)
  const currentDisplayRef = useRef<DisplayPhoto | null>(null);
  const queueIndexRef = useRef(0);
  const photoQueueRef = useRef<QueuedPhoto[]>([]);

  // Slideshow navigation
  const showSlideshow = currentDisplay === null;
  const showCountdown = activeCountdowns > 0;
  const slideshowPaused = showCountdown || !showSlideshow;

  const {
    currentPhoto: slideshowPhoto,
    goNext,
    goPrevious,
    skip,
    toggleMode,
    refresh: refreshSlideshow,
  } = useSlideshowNavigation({
    intervalMs: slideshowIntervalMs,
    paused: slideshowPaused,
  });

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

  // Keep refs in sync with state
  useEffect(() => {
    currentDisplayRef.current = currentDisplay;
  }, [currentDisplay]);
  useEffect(() => { queueIndexRef.current = queueIndex; }, [queueIndex]);
  useEffect(() => { photoQueueRef.current = photoQueue; }, [photoQueue]);

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

      // If it was from queue, advance to next or clear queue
      if (fromQueue) {
        const nextIdx = queueIndexRef.current + 1;
        if (nextIdx >= photoQueueRef.current.length) {
          setPhotoQueue([]);
          setQueueIndex(0);
        } else {
          setQueueIndex(nextIdx);
        }
      }

      // Clear display to trigger showing next from queue or slideshow
      setCurrentDisplay(null);

      // Refresh slideshow to include the newly captured photo
      refreshSlideshow();
    }, slideshowIntervalMs);
  }, [refreshSlideshow, slideshowIntervalMs]);

  // Show next photo from queue
  const showNextFromQueue = useCallback(() => {
    const idx = queueIndexRef.current;
    const queue = photoQueueRef.current;
    if (idx < queue.length) {
      startShowingPhoto(queue[idx], true);
    }
  }, [startShowingPhoto]);

  // Process queue when conditions change
  useEffect(() => {
    // Start showing queue when no countdowns and nothing currently displayed
    if (activeCountdowns === 0 && currentDisplay === null
        && photoQueue.length > 0 && queueIndex < photoQueue.length) {
      // Defer to avoid synchronous setState within effect
      const timeoutId = setTimeout(showNextFromQueue, 0);
      return () => clearTimeout(timeoutId);
    }
  }, [activeCountdowns, currentDisplay, photoQueue.length, queueIndex, showNextFromQueue]);

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

  const handleTrigger = useCallback(async (durationMs?: number) => {
    resetWatchdog();
    console.log('Triggering capture...', durationMs ? `duration: ${durationMs}ms` : '(default duration)');

    try {
      await triggerCapture(durationMs);
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
  }, [resetWatchdog]);

  // Clear queue and dismiss current display
  const clearQueueAndDisplay = useCallback(() => {
    if (previewTimeoutRef.current !== null) {
      clearTimeout(previewTimeoutRef.current);
      previewTimeoutRef.current = null;
    }
    setPhotoQueue([]);
    setQueueIndex(0);
    setCurrentDisplay(null);
    refreshSlideshow();
  }, [refreshSlideshow]);

  // Queue navigation functions
  const navigateQueueForward = useCallback(() => {
    if (previewTimeoutRef.current !== null) {
      clearTimeout(previewTimeoutRef.current);
    }
    const nextIdx = queueIndexRef.current + 1;
    const queue = photoQueueRef.current;
    if (nextIdx >= queue.length) {
      clearQueueAndDisplay();
    } else {
      setQueueIndex(nextIdx);
      startShowingPhoto(queue[nextIdx], true);
    }
  }, [startShowingPhoto, clearQueueAndDisplay]);

  const navigateQueueBackward = useCallback(() => {
    if (queueIndexRef.current <= 0) return;
    if (previewTimeoutRef.current !== null) {
      clearTimeout(previewTimeoutRef.current);
    }
    const prevIdx = queueIndexRef.current - 1;
    setQueueIndex(prevIdx);
    startShowingPhoto(photoQueueRef.current[prevIdx], true);
  }, [startShowingPhoto]);

  // Dismiss a non-queue photo (just-captured), letting queue processing pick up next
  const dismissCurrentPhoto = useCallback(() => {
    if (previewTimeoutRef.current !== null) {
      clearTimeout(previewTimeoutRef.current);
      previewTimeoutRef.current = null;
    }
    setCurrentDisplay(null);
    refreshSlideshow();
  }, [refreshSlideshow]);

  // Wrapped callbacks: navigate queue when showing a photo, pass through to slideshow otherwise
  const handleNavNext = useCallback(() => {
    if (currentDisplayRef.current?.fromQueue) {
      navigateQueueForward();
    } else if (currentDisplayRef.current !== null) {
      dismissCurrentPhoto();
    } else {
      goNext();
    }
  }, [navigateQueueForward, dismissCurrentPhoto, goNext]);

  const handleNavPrevious = useCallback(() => {
    if (currentDisplayRef.current?.fromQueue) {
      navigateQueueBackward();
    } else if (currentDisplayRef.current === null) {
      goPrevious();
    }
    // Non-queue photo showing: no "previous" exists, ignore
  }, [navigateQueueBackward, goPrevious]);

  // Other nav actions clear any displayed photo/queue, then perform the slideshow action
  const handleNavSkipForward = useCallback(() => {
    if (currentDisplayRef.current !== null || photoQueueRef.current.length > 0) clearQueueAndDisplay();
    skip(10);
  }, [clearQueueAndDisplay, skip]);

  const handleNavSkipBackward = useCallback(() => {
    if (currentDisplayRef.current !== null || photoQueueRef.current.length > 0) clearQueueAndDisplay();
    skip(-10);
  }, [clearQueueAndDisplay, skip]);

  const handleNavToggleMode = useCallback(() => {
    if (currentDisplayRef.current !== null || photoQueueRef.current.length > 0) clearQueueAndDisplay();
    toggleMode();
  }, [clearQueueAndDisplay, toggleMode]);

  // Keyboard navigation - disabled during countdown
  useKeyboardNavigation({
    onNext: handleNavNext,
    onPrevious: handleNavPrevious,
    onSkipForward: handleNavSkipForward,
    onSkipBackward: handleNavSkipBackward,
    onToggleMode: handleNavToggleMode,
    onTriggerCapture: handleTrigger,
    enabled: !showCountdown,
  });

  const handleGamepadDebugEvent = useCallback((event: GamepadDebugEvent) => {
    setGamepadDebugEvent(event);
    if (gamepadDebugTimeoutRef.current !== null) {
      clearTimeout(gamepadDebugTimeoutRef.current);
    }
    gamepadDebugTimeoutRef.current = window.setTimeout(() => {
      setGamepadDebugEvent(null);
    }, GAMEPAD_DEBUG_DISPLAY_MS);
  }, []);

  // Gamepad navigation - disabled during countdown, enabled only when gamepadConfig.enabled
  useGamepadNavigation({
    onNext: handleNavNext,
    onPrevious: handleNavPrevious,
    onSkipForward: handleNavSkipForward,
    onSkipBackward: handleNavSkipBackward,
    onToggleMode: handleNavToggleMode,
    onTriggerCapture: handleTrigger,
    enabled: !showCountdown && (gamepadConfig?.enabled ?? false),
    debugMode: gamepadConfig?.debugMode ?? false,
    buttons: gamepadConfig?.buttons,
    dpadAxes: gamepadConfig?.dpadAxes,
    onDebugEvent: handleGamepadDebugEvent,
  });

  const handleClick = () => {
    handleTrigger();
  };

  const handleCountdownComplete = () => {
    // Server handles actual capture
  };

  // Determine what to show
  const showCapturedPhoto = currentDisplay !== null;
  const showError = errorMessage !== null;

  return (
    <div className="booth-page" onClick={handleClick}>
      {/* Show slideshow when not showing captured photos */}
      {showSlideshow && <Slideshow photo={slideshowPhoto} qrCodeBaseUrl={qrCodeBaseUrl} swirlEffect={swirlEffect} />}

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
              swirlEffect={swirlEffect}
              fadingOut
            />
          )}
          <PhotoDisplay
            key={currentDisplay.key}
            photoId={currentDisplay.photo.photoId}
            code={currentDisplay.photo.code}
            kenBurns={currentDisplay.kenBurns}
            qrCodeBaseUrl={qrCodeBaseUrl}
            swirlEffect={swirlEffect}
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

      {/* Gamepad debug overlay */}
      {gamepadConfig?.debugMode && gamepadDebugEvent && (
        <div className="gamepad-debug">
          {gamepadDebugEvent.buttonIndex !== undefined
            ? <span>Button {gamepadDebugEvent.buttonIndex}</span>
            : <span>Axis {gamepadDebugEvent.axisIndex}</span>}
          <span>{gamepadDebugEvent.action}</span>
        </div>
      )}
    </div>
  );
}

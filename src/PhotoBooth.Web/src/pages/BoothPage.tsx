import { useState, useCallback, useEffect, useRef } from 'react';
import { Slideshow } from '../components/Slideshow';
import { CaptureOverlay } from '../components/CaptureOverlay';
import { PhotoDisplay } from '../components/PhotoDisplay';
import { useEventStream } from '../api/events';
import { triggerCapture } from '../api/client';
import type { PhotoBoothEvent, PhotoCapturedEvent } from '../api/types';

type BoothState =
  | { mode: 'slideshow' }
  | { mode: 'countdown'; durationMs: number }
  | { mode: 'preview'; photo: PhotoCapturedEvent }
  | { mode: 'error'; message: string };

const CAPTURE_TIMEOUT_MS = 15000; // Max time to wait for capture result
const WATCHDOG_RELOAD_MS = 5 * 60 * 1000; // Reload page after 5 minutes of no interaction

export function BoothPage() {
  const [state, setState] = useState<BoothState>({ mode: 'slideshow' });
  const captureTimeoutRef = useRef<number | null>(null);
  const watchdogTimeoutRef = useRef<number | null>(null);

  // Clear any pending capture timeout
  const clearCaptureTimeout = () => {
    if (captureTimeoutRef.current !== null) {
      clearTimeout(captureTimeoutRef.current);
      captureTimeoutRef.current = null;
    }
  };

  // Reset the watchdog timer (call this on any user interaction)
  const resetWatchdog = useCallback(() => {
    if (watchdogTimeoutRef.current !== null) {
      clearTimeout(watchdogTimeoutRef.current);
    }
    watchdogTimeoutRef.current = window.setTimeout(() => {
      console.log('Watchdog: No interaction for 5 minutes, reloading page...');
      window.location.reload();
    }, WATCHDOG_RELOAD_MS);
  }, []);

  // Initialize watchdog on mount
  useEffect(() => {
    resetWatchdog();
    return () => {
      if (watchdogTimeoutRef.current !== null) {
        clearTimeout(watchdogTimeoutRef.current);
      }
    };
  }, [resetWatchdog]);

  const handleEvent = useCallback((event: PhotoBoothEvent) => {
    clearCaptureTimeout();

    switch (event.eventType) {
      case 'countdown-started':
        setState({ mode: 'countdown', durationMs: event.durationMs });
        // Set a timeout to recover if we don't get a result
        captureTimeoutRef.current = window.setTimeout(() => {
          console.warn('Capture timeout - showing error');
          setState({ mode: 'error', message: 'Capture timed out' });
          setTimeout(() => {
            console.log('Timeout recovery - returning to slideshow');
            setState({ mode: 'slideshow' });
          }, 3000);
        }, CAPTURE_TIMEOUT_MS);
        break;
      case 'photo-captured':
        setState({ mode: 'preview', photo: event });
        // Return to slideshow after preview
        setTimeout(() => setState({ mode: 'slideshow' }), 5000);
        break;
      case 'capture-failed':
        setState({ mode: 'error', message: event.error });
        setTimeout(() => setState({ mode: 'slideshow' }), 3000);
        break;
    }
  }, []);

  useEventStream(handleEvent);

  const handleTrigger = async () => {
    resetWatchdog(); // User interaction - reset watchdog
    console.log('Click detected, current mode:', state.mode);

    if (state.mode !== 'slideshow') {
      console.log('Ignoring click - not in slideshow mode');
      return;
    }

    try {
      console.log('Triggering capture...');
      await triggerCapture();
    } catch (err) {
      console.error('Trigger error:', err);
      if (err instanceof Error && err.message !== 'Capture already in progress') {
        setState({ mode: 'error', message: err.message });
        setTimeout(() => setState({ mode: 'slideshow' }), 3000);
      }
    }
  };

  const handleCountdownComplete = () => {
    // The server handles the actual capture, we just wait for the event
  };

  return (
    <div className="booth-page" onClick={handleTrigger}>
      {state.mode === 'slideshow' && <Slideshow paused={false} />}

      {state.mode === 'countdown' && (
        <>
          <Slideshow paused={true} />
          <CaptureOverlay durationMs={state.durationMs} onComplete={handleCountdownComplete} />
        </>
      )}

      {state.mode === 'preview' && (
        <div className="preview">
          <PhotoDisplay photoId={state.photo.photoId} code={state.photo.code} />
        </div>
      )}

      {state.mode === 'error' && (
        <div className="error-display">
          <div className="error-message">{state.message}</div>
        </div>
      )}
    </div>
  );
}

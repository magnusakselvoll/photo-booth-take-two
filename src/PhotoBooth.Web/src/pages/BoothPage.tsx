import { useState, useCallback } from 'react';
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

export function BoothPage() {
  const [state, setState] = useState<BoothState>({ mode: 'slideshow' });

  const handleEvent = useCallback((event: PhotoBoothEvent) => {
    switch (event.eventType) {
      case 'countdown-started':
        setState({ mode: 'countdown', durationMs: event.durationMs });
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
    if (state.mode !== 'slideshow') return;

    try {
      await triggerCapture();
    } catch (err) {
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

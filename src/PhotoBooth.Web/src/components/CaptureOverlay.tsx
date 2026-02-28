import { useState, useEffect } from 'react';

interface CaptureOverlayProps {
  durationMs: number;
  onComplete: () => void;
}

export function CaptureOverlay({ durationMs, onComplete }: CaptureOverlayProps) {
  const [secondsRemaining, setSecondsRemaining] = useState(Math.ceil(durationMs / 1000));
  const [waitingForCapture, setWaitingForCapture] = useState(false);

  useEffect(() => {
    if (secondsRemaining <= 0) {
      onComplete();
      return;
    }

    const timer = setTimeout(() => {
      setSecondsRemaining((s) => s - 1);
    }, 1000);

    return () => clearTimeout(timer);
  }, [secondsRemaining, onComplete]);

  useEffect(() => {
    if (secondsRemaining > 0) return;

    const timer = setTimeout(() => {
      setWaitingForCapture(true);
    }, 500);

    return () => clearTimeout(timer);
  }, [secondsRemaining]);

  return (
    <div className="capture-overlay">
      {secondsRemaining > 0 ? (
        <div className="countdown">{secondsRemaining}</div>
      ) : !waitingForCapture ? (
        <div className="countdown">Smile!</div>
      ) : (
        <div className="waiting-message">Developing photo...</div>
      )}
    </div>
  );
}

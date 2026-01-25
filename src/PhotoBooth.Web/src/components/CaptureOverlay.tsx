import { useState, useEffect } from 'react';

interface CaptureOverlayProps {
  durationMs: number;
  onComplete: () => void;
}

export function CaptureOverlay({ durationMs, onComplete }: CaptureOverlayProps) {
  const [secondsRemaining, setSecondsRemaining] = useState(Math.ceil(durationMs / 1000));

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

  return (
    <div className="capture-overlay">
      <div className="countdown">{secondsRemaining > 0 ? secondsRemaining : 'Smile!'}</div>
    </div>
  );
}

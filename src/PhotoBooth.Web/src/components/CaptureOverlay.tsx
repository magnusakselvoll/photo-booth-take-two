import { useState, useEffect, useMemo } from 'react';

const SMILE_PHRASES = [
  'Smile!',
  'Cheese!',
  'Say cheese!',
  'Sonrie!',
  'Looking good!',
  'Guapo!',
  'Guapa!',
  'Gorgeous!',
  'Diga whisky!',
  'Fabulous!',
  'Say aah!',
  'Que guapo!',
  'Que guapa!',
  'Hermoso!',
  'Hermosa!',
  'You look amazing!',
  'Stunning!',
  'Selfie time!',
  'Work it!',
  'Fierce!',
  'Ole!',
  'Bellisimo!',
  'Bellisima!',
  'Say "yeah!"',
  'Dazzling!',
  'Preciosa!',
  'Precioso!',
  'Vogue!',
  'Slay!',
  'Yes queen!',
  'Strike a pose!',
  'Wow!',
];

const DEVELOPING_PHRASES = [
  'Developing photo...',
  'Working some magic...',
  'Creando magia...',
  'Almost there...',
  'Un momento...',
  'Patience, beauties...',
  'Creating a masterpiece...',
  'Uno momento, por favor...',
  'Good things take time...',
  'Casi listo...',
  'Making you look fabulous...',
  'Hold that pose...',
];

const COUNTDOWN_SUBSTITUTIONS: Record<number, readonly [string, string, string]> = {
  1: ['one', 'uno', '📷'],
  2: ['two', 'dos', '⭐'],
  3: ['three', 'tres', '✨'],
  4: ['four', 'cuatro', '🔥'],
  5: ['five', 'cinco', '🎉'],
  6: ['six', 'seis', '✌️'],
  7: ['seven', 'siete', '😎'],
  8: ['eight', 'ocho', '🎱'],
  9: ['nine', 'nueve', '🎶'],
  10: ['ten', 'diez', '💯'],
  11: ['eleven', 'once', '⚡'],
  12: ['twelve', 'doce', '🎁'],
  13: ['thirteen', 'trece', '🍀'],
  14: ['fourteen', 'catorce', '💪'],
  15: ['fifteen', 'quince', '🌟'],
  16: ['sixteen', 'dieciseis', '🎊'],
  17: ['seventeen', 'diecisiete', '🚀'],
  18: ['eighteen', 'dieciocho', '🎭'],
  19: ['nineteen', 'diecinueve', '🌈'],
  20: ['twenty', 'veinte', '🏆'],
};

function pickRandom<T>(arr: readonly T[]): T {
  return arr[Math.floor(Math.random() * arr.length)];
}

interface CaptureOverlayProps {
  durationMs: number;
  onComplete: () => void;
}

export function CaptureOverlay({ durationMs, onComplete }: CaptureOverlayProps) {
  const [secondsRemaining, setSecondsRemaining] = useState(Math.ceil(durationMs / 1000));
  const [waitingForCapture, setWaitingForCapture] = useState(false);

  const smilePhrase = useMemo(() => pickRandom(SMILE_PHRASES), []);
  const developingPhrase = useMemo(() => pickRandom(DEVELOPING_PHRASES), []);

  const countdownDisplays = useMemo(() => {
    const maxSeconds = Math.ceil(durationMs / 1000);
    const displays: Record<number, string> = {};
    for (let s = 1; s <= maxSeconds; s++) {
      const subs = COUNTDOWN_SUBSTITUTIONS[s];
      if (subs && Math.random() < 0.05) {
        displays[s] = pickRandom(subs);
      } else {
        displays[s] = String(s);
      }
    }
    return displays;
  }, [durationMs]);

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
        <div className="countdown">{countdownDisplays[secondsRemaining] ?? String(secondsRemaining)}</div>
      ) : !waitingForCapture ? (
        <div className="countdown">{smilePhrase}</div>
      ) : (
        <div className="waiting-message">{developingPhrase}</div>
      )}
    </div>
  );
}

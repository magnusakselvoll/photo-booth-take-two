import { useCallback, useEffect, useRef, useState } from 'react';

interface UseIdleCursorOptions {
  timeoutMs?: number;
  enabled?: boolean;
}

export function useIdleCursor({ timeoutMs = 3000, enabled = true }: UseIdleCursorOptions = {}): boolean {
  const [hidden, setHidden] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimer = useCallback(() => {
    if (timeoutRef.current !== null) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  const startTimer = useCallback(() => {
    clearTimer();
    timeoutRef.current = setTimeout(() => {
      setHidden(true);
    }, timeoutMs);
  }, [clearTimer, timeoutMs]);

  const handleMouseMove = useCallback(() => {
    setHidden(false);
    startTimer();
  }, [startTimer]);

  useEffect(() => {
    if (!enabled) {
      clearTimer();
      setHidden(false);
      return;
    }

    startTimer();
    document.addEventListener('mousemove', handleMouseMove);

    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      clearTimer();
    };
  }, [enabled, startTimer, handleMouseMove, clearTimer]);

  return hidden;
}

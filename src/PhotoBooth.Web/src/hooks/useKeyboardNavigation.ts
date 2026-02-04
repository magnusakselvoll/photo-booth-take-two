import { useEffect, useCallback } from 'react';

export interface KeyboardNavigationConfig {
  onNext?: () => void;
  onPrevious?: () => void;
  onSkipForward?: () => void;
  onSkipBackward?: () => void;
  onToggleMode?: () => void;
  onTriggerCapture?: (durationMs?: number) => void;
  enabled?: boolean;
}

export function useKeyboardNavigation({
  onNext,
  onPrevious,
  onSkipForward,
  onSkipBackward,
  onToggleMode,
  onTriggerCapture,
  enabled = true,
}: KeyboardNavigationConfig): void {
  const handleKeyDown = useCallback((event: KeyboardEvent) => {
    if (!enabled) return;

    // Ignore if focus is on an input element
    if (
      event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement
    ) {
      return;
    }

    switch (event.key) {
      case 'ArrowRight':
        event.preventDefault();
        onNext?.();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        onPrevious?.();
        break;
      case 'ArrowDown':
        event.preventDefault();
        onSkipForward?.();
        break;
      case 'ArrowUp':
        event.preventDefault();
        onSkipBackward?.();
        break;
      case 'r':
      case 'R':
        event.preventDefault();
        onToggleMode?.();
        break;
      case ' ':
      case 'Enter':
        event.preventDefault();
        onTriggerCapture?.();
        break;
      case '1':
        event.preventDefault();
        onTriggerCapture?.(1000);
        break;
      case '3':
        event.preventDefault();
        onTriggerCapture?.(3000);
        break;
      case '5':
        event.preventDefault();
        onTriggerCapture?.(5000);
        break;
    }
  }, [enabled, onNext, onPrevious, onSkipForward, onSkipBackward, onToggleMode, onTriggerCapture]);

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [handleKeyDown]);
}

import { describe, it, expect, vi, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useKeyboardNavigation } from '../useKeyboardNavigation';

function fireKey(key: string, target?: EventTarget) {
  const event = new KeyboardEvent('keydown', { key, bubbles: true });
  if (target) {
    Object.defineProperty(event, 'target', { value: target });
  }
  window.dispatchEvent(event);
}

describe('useKeyboardNavigation', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('calls onNext on ArrowRight', () => {
    const onNext = vi.fn();
    renderHook(() => useKeyboardNavigation({ onNext }));

    fireKey('ArrowRight');
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('calls onPrevious on ArrowLeft', () => {
    const onPrevious = vi.fn();
    renderHook(() => useKeyboardNavigation({ onPrevious }));

    fireKey('ArrowLeft');
    expect(onPrevious).toHaveBeenCalledOnce();
  });

  it('calls onSkipForward on ArrowDown', () => {
    const onSkipForward = vi.fn();
    renderHook(() => useKeyboardNavigation({ onSkipForward }));

    fireKey('ArrowDown');
    expect(onSkipForward).toHaveBeenCalledOnce();
  });

  it('calls onSkipBackward on ArrowUp', () => {
    const onSkipBackward = vi.fn();
    renderHook(() => useKeyboardNavigation({ onSkipBackward }));

    fireKey('ArrowUp');
    expect(onSkipBackward).toHaveBeenCalledOnce();
  });

  it('calls onToggleMode on R key', () => {
    const onToggleMode = vi.fn();
    renderHook(() => useKeyboardNavigation({ onToggleMode }));

    fireKey('r');
    expect(onToggleMode).toHaveBeenCalledOnce();

    fireKey('R');
    expect(onToggleMode).toHaveBeenCalledTimes(2);
  });

  it('calls onTriggerCapture on Space', () => {
    const onTriggerCapture = vi.fn();
    renderHook(() => useKeyboardNavigation({ onTriggerCapture }));

    fireKey(' ');
    expect(onTriggerCapture).toHaveBeenCalledWith();
  });

  it('calls onTriggerCapture on Enter', () => {
    const onTriggerCapture = vi.fn();
    renderHook(() => useKeyboardNavigation({ onTriggerCapture }));

    fireKey('Enter');
    expect(onTriggerCapture).toHaveBeenCalledWith();
  });

  it('calls onTriggerCapture with 1000 on key 1', () => {
    const onTriggerCapture = vi.fn();
    renderHook(() => useKeyboardNavigation({ onTriggerCapture }));

    fireKey('1');
    expect(onTriggerCapture).toHaveBeenCalledWith(1000);
  });

  it('calls onTriggerCapture with 3000 on key 3', () => {
    const onTriggerCapture = vi.fn();
    renderHook(() => useKeyboardNavigation({ onTriggerCapture }));

    fireKey('3');
    expect(onTriggerCapture).toHaveBeenCalledWith(3000);
  });

  it('calls onTriggerCapture with 5000 on key 5', () => {
    const onTriggerCapture = vi.fn();
    renderHook(() => useKeyboardNavigation({ onTriggerCapture }));

    fireKey('5');
    expect(onTriggerCapture).toHaveBeenCalledWith(5000);
  });

  it('ignores keys when target is an input element', () => {
    const onNext = vi.fn();
    renderHook(() => useKeyboardNavigation({ onNext }));

    const input = document.createElement('input');
    fireKey('ArrowRight', input);
    expect(onNext).not.toHaveBeenCalled();
  });

  it('ignores keys when target is a textarea element', () => {
    const onNext = vi.fn();
    renderHook(() => useKeyboardNavigation({ onNext }));

    const textarea = document.createElement('textarea');
    fireKey('ArrowRight', textarea);
    expect(onNext).not.toHaveBeenCalled();
  });

  it('does nothing when enabled is false', () => {
    const onNext = vi.fn();
    const onPrevious = vi.fn();
    const onTriggerCapture = vi.fn();
    renderHook(() =>
      useKeyboardNavigation({ onNext, onPrevious, onTriggerCapture, enabled: false }),
    );

    fireKey('ArrowRight');
    fireKey('ArrowLeft');
    fireKey(' ');
    expect(onNext).not.toHaveBeenCalled();
    expect(onPrevious).not.toHaveBeenCalled();
    expect(onTriggerCapture).not.toHaveBeenCalled();
  });

  it('cleans up event listener on unmount', () => {
    const onNext = vi.fn();
    const { unmount } = renderHook(() => useKeyboardNavigation({ onNext }));

    unmount();
    fireKey('ArrowRight');
    expect(onNext).not.toHaveBeenCalled();
  });
});

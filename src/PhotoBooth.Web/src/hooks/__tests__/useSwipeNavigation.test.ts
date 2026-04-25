import { describe, it, expect, vi, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSwipeNavigation } from '../useSwipeNavigation';

function makeTouchLike(clientX: number, clientY: number) {
  return { clientX, clientY, identifier: 1 };
}

function dispatchTouch(
  el: HTMLElement,
  type: string,
  touches: Array<{ clientX: number; clientY: number; identifier: number }>,
  changedTouches?: Array<{ clientX: number; clientY: number; identifier: number }>,
) {
  const event = new Event(type, { bubbles: true, cancelable: true }) as unknown as TouchEvent;
  const touchesArr = Object.assign([...touches], { length: touches.length, item: (i: number) => touches[i] });
  const changedArr = Object.assign([...(changedTouches ?? touches)], {
    length: (changedTouches ?? touches).length,
    item: (i: number) => (changedTouches ?? touches)[i],
  });
  Object.defineProperty(event, 'touches', { value: touchesArr });
  Object.defineProperty(event, 'changedTouches', { value: changedArr });
  el.dispatchEvent(event);
}

function swipeLeft(el: HTMLElement) {
  const start = makeTouchLike(200, 100);
  const end = makeTouchLike(80, 100);
  dispatchTouch(el, 'touchstart', [start]);
  dispatchTouch(el, 'touchmove', [end]);
  dispatchTouch(el, 'touchend', [], [end]);
  el.dispatchEvent(new Event('transitionend', { bubbles: false }));
}

function swipeRight(el: HTMLElement) {
  const start = makeTouchLike(80, 100);
  const end = makeTouchLike(200, 100);
  dispatchTouch(el, 'touchstart', [start]);
  dispatchTouch(el, 'touchmove', [end]);
  dispatchTouch(el, 'touchend', [], [end]);
  el.dispatchEvent(new Event('transitionend', { bubbles: false }));
}

describe('useSwipeNavigation', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('calls onSwipeLeft when swiping left beyond threshold', () => {
    const el = document.createElement('div');
    document.body.appendChild(el);
    const elementRef = { current: el };
    const onSwipeLeft = vi.fn();

    renderHook(() => useSwipeNavigation({ onSwipeLeft, elementRef }));
    act(() => swipeLeft(el));

    expect(onSwipeLeft).toHaveBeenCalledOnce();
    document.body.removeChild(el);
  });

  it('calls onSwipeRight when swiping right beyond threshold', () => {
    const el = document.createElement('div');
    document.body.appendChild(el);
    const elementRef = { current: el };
    const onSwipeRight = vi.fn();

    renderHook(() => useSwipeNavigation({ onSwipeRight, elementRef }));
    act(() => swipeRight(el));

    expect(onSwipeRight).toHaveBeenCalledOnce();
    document.body.removeChild(el);
  });

  it('does not call callbacks when disabled is true', () => {
    const el = document.createElement('div');
    document.body.appendChild(el);
    const elementRef = { current: el };
    const onSwipeLeft = vi.fn();
    const onSwipeRight = vi.fn();

    renderHook(() => useSwipeNavigation({ onSwipeLeft, onSwipeRight, elementRef, disabled: true }));
    act(() => {
      swipeLeft(el);
      swipeRight(el);
    });

    expect(onSwipeLeft).not.toHaveBeenCalled();
    expect(onSwipeRight).not.toHaveBeenCalled();
    document.body.removeChild(el);
  });

  it('does not call callbacks when touch starts with multiple fingers', () => {
    const el = document.createElement('div');
    document.body.appendChild(el);
    const elementRef = { current: el };
    const onSwipeLeft = vi.fn();

    renderHook(() => useSwipeNavigation({ onSwipeLeft, elementRef }));
    act(() => {
      const t1 = makeTouchLike(200, 100);
      const t2 = makeTouchLike(250, 150);
      const end = makeTouchLike(80, 100);
      dispatchTouch(el, 'touchstart', [t1, t2]);
      dispatchTouch(el, 'touchmove', [end, t2]);
      dispatchTouch(el, 'touchend', [], [end]);
      el.dispatchEvent(new Event('transitionend', { bubbles: false }));
    });

    expect(onSwipeLeft).not.toHaveBeenCalled();
    document.body.removeChild(el);
  });

  it('cleans up event listeners on unmount', () => {
    const el = document.createElement('div');
    document.body.appendChild(el);
    const elementRef = { current: el };
    const onSwipeLeft = vi.fn();

    const { unmount } = renderHook(() => useSwipeNavigation({ onSwipeLeft, elementRef }));
    unmount();
    act(() => swipeLeft(el));

    expect(onSwipeLeft).not.toHaveBeenCalled();
    document.body.removeChild(el);
  });
});

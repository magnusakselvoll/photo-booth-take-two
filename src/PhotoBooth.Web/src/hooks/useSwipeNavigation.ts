import { useEffect, useCallback, useRef } from 'react';

const SWIPE_THRESHOLD = 80;
const SLIDE_DURATION_MS = 280;

export interface SwipeNavigationConfig {
  onSwipeLeft?: () => void;
  onSwipeRight?: () => void;
  elementRef: React.RefObject<HTMLElement | null>;
  disabled?: boolean;
}

export function useSwipeNavigation({ onSwipeLeft, onSwipeRight, elementRef, disabled }: SwipeNavigationConfig): void {
  const touchStartX = useRef<number | null>(null);
  const touchStartY = useRef<number | null>(null);
  const isHorizontalSwipe = useRef<boolean | null>(null);

  // Use refs for callbacks so handlers don't need to be recreated when prev/next changes
  const onSwipeLeftRef = useRef(onSwipeLeft);
  const onSwipeRightRef = useRef(onSwipeRight);
  const disabledRef = useRef(disabled);

  useEffect(() => {
    onSwipeLeftRef.current = onSwipeLeft;
    onSwipeRightRef.current = onSwipeRight;
    disabledRef.current = disabled;
  }, [onSwipeLeft, onSwipeRight, disabled]);

  const handleTouchStart = useCallback((event: TouchEvent) => {
    if (disabledRef.current || event.touches.length > 1) return;

    const touch = event.touches[0];
    touchStartX.current = touch.clientX;
    touchStartY.current = touch.clientY;
    isHorizontalSwipe.current = null;

    const el = elementRef.current;
    if (el) el.style.transition = 'none';
  }, [elementRef]);

  const handleTouchMove = useCallback((event: TouchEvent) => {
    if (disabledRef.current || event.touches.length > 1) return;
    if (touchStartX.current === null || touchStartY.current === null) return;

    const touch = event.touches[0];
    const deltaX = touch.clientX - touchStartX.current;
    const deltaY = touch.clientY - touchStartY.current;

    if (isHorizontalSwipe.current === null) {
      if (Math.abs(deltaX) < 5 && Math.abs(deltaY) < 5) return;
      isHorizontalSwipe.current = Math.abs(deltaX) > Math.abs(deltaY);
    }

    if (!isHorizontalSwipe.current) return;

    const canSwipe = deltaX < 0 ? !!onSwipeLeftRef.current : !!onSwipeRightRef.current;
    if (!canSwipe) return;

    event.preventDefault();
    event.stopPropagation();

    const el = elementRef.current;
    if (el) el.style.transform = `translateX(${deltaX}px)`;
  }, [elementRef]);

  const handleTouchEnd = useCallback((event: TouchEvent) => {
    if (disabledRef.current) {
      touchStartX.current = null;
      touchStartY.current = null;
      isHorizontalSwipe.current = null;
      return;
    }
    if (touchStartX.current === null || touchStartY.current === null || isHorizontalSwipe.current !== true) {
      touchStartX.current = null;
      touchStartY.current = null;
      isHorizontalSwipe.current = null;
      return;
    }

    const touch = event.changedTouches[0];
    const deltaX = touch.clientX - touchStartX.current;
    touchStartX.current = null;
    touchStartY.current = null;
    isHorizontalSwipe.current = null;

    const el = elementRef.current;
    if (!el) return;

    const callback = deltaX < 0 ? onSwipeLeftRef.current : onSwipeRightRef.current;

    if (Math.abs(deltaX) >= SWIPE_THRESHOLD && callback) {
      const targetOffset = deltaX < 0 ? -el.offsetWidth : el.offsetWidth;
      el.style.transition = `transform ${SLIDE_DURATION_MS}ms ease`;
      el.style.transform = `translateX(${targetOffset}px)`;
      el.addEventListener('transitionend', () => {
        callback();
        el.style.transition = 'none';
        el.style.transform = '';
      }, { once: true });
    } else {
      el.style.transition = `transform ${SLIDE_DURATION_MS}ms ease`;
      el.style.transform = '';
      el.addEventListener('transitionend', () => {
        el.style.transition = '';
      }, { once: true });
    }
  }, [elementRef]);

  useEffect(() => {
    const element = elementRef.current;
    if (!element) return;

    element.addEventListener('touchstart', handleTouchStart, { passive: true, capture: true });
    element.addEventListener('touchmove', handleTouchMove, { passive: false, capture: true });
    element.addEventListener('touchend', handleTouchEnd, { passive: true, capture: true });

    return () => {
      element.removeEventListener('touchstart', handleTouchStart, { capture: true });
      element.removeEventListener('touchmove', handleTouchMove, { capture: true });
      element.removeEventListener('touchend', handleTouchEnd, { capture: true });
      element.style.transform = '';
      element.style.transition = '';
    };
  }, [elementRef, handleTouchStart, handleTouchMove, handleTouchEnd]);
}

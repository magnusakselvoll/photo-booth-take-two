import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor, cleanup } from '@testing-library/react';
import { useSlideshowNavigation } from '../useSlideshowNavigation';
import type { PhotoDto } from '../../api/types';

const mockPhotos: PhotoDto[] = [
  { id: 'photo-1', code: '001', capturedAt: '2025-01-01T00:00:00Z' },
  { id: 'photo-2', code: '002', capturedAt: '2025-01-01T00:01:00Z' },
  { id: 'photo-3', code: '003', capturedAt: '2025-01-01T00:02:00Z' },
  { id: 'photo-4', code: '004', capturedAt: '2025-01-01T00:03:00Z' },
  { id: 'photo-5', code: '005', capturedAt: '2025-01-01T00:04:00Z' },
];

const mockGetAllPhotos = vi.fn<() => Promise<PhotoDto[]>>();

vi.mock('../../api/client', () => ({
  getAllPhotos: (...args: unknown[]) => mockGetAllPhotos(...(args as [])),
}));

describe('useSlideshowNavigation', () => {
  beforeEach(() => {
    mockGetAllPhotos.mockResolvedValue(mockPhotos);
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('loads photos on mount', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });
    expect(result.current.currentPhoto).not.toBeNull();
  });

  it('returns null currentPhoto when no photos', async () => {
    mockGetAllPhotos.mockResolvedValue([]);

    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(0);
    });
    expect(result.current.currentPhoto).toBeNull();
  });

  it('goNext wraps around correctly in sequential mode', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    for (let i = 0; i < 5; i++) {
      act(() => result.current.goNext());
    }

    expect(result.current.currentIndex).toBe(0);
  });

  it('goPrevious wraps around correctly in sequential mode', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    act(() => result.current.goPrevious());
    expect(result.current.currentIndex).toBe(4);
  });

  it('skip moves by n positions', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    act(() => result.current.skip(3));
    expect(result.current.currentIndex).toBe(3);
  });

  it('skip wraps around with negative values', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    act(() => result.current.skip(-2));
    expect(result.current.currentIndex).toBe(3);
  });

  it('toggleMode switches between random and sequential', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    expect(result.current.isRandom).toBe(false);

    act(() => result.current.toggleMode());
    expect(result.current.isRandom).toBe(true);

    act(() => result.current.toggleMode());
    expect(result.current.isRandom).toBe(false);
  });

  it('toggleMode preserves current photo', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    act(() => result.current.skip(2));
    const photoBeforeToggle = result.current.currentPhoto;

    act(() => result.current.toggleMode());
    expect(result.current.currentPhoto).toEqual(photoBeforeToggle);
  });

  it('refresh fetches new photos and stays on same photo by ID', async () => {
    const { result } = renderHook(() =>
      useSlideshowNavigation({ paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    act(() => result.current.skip(2));
    expect(result.current.currentPhoto?.id).toBe('photo-3');

    const updatedPhotos: PhotoDto[] = [
      { id: 'photo-new', code: '000', capturedAt: '2024-12-31T00:00:00Z' },
      ...mockPhotos,
    ];
    mockGetAllPhotos.mockResolvedValue(updatedPhotos);

    await act(() => result.current.refresh());

    expect(result.current.currentPhoto?.id).toBe('photo-3');
    expect(result.current.totalPhotos).toBe(6);
  });

  it('auto-advance timer fires goNext', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });

    const { result } = renderHook(() =>
      useSlideshowNavigation({ intervalMs: 1000, paused: false, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    const initialIndex = result.current.currentIndex;

    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(result.current.currentIndex).toBe((initialIndex + 1) % 5);

    vi.useRealTimers();
  });

  it('auto-advance timer does not fire when paused', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });

    const { result } = renderHook(() =>
      useSlideshowNavigation({ intervalMs: 1000, paused: true, initialRandom: false }),
    );

    await waitFor(() => {
      expect(result.current.totalPhotos).toBe(5);
    });

    const initialIndex = result.current.currentIndex;

    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(result.current.currentIndex).toBe(initialIndex);

    vi.useRealTimers();
  });
});

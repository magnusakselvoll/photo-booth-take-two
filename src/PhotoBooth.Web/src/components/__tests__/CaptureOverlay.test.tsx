import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, act } from '@testing-library/react';
import { CaptureOverlay } from '../CaptureOverlay';

describe('CaptureOverlay', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it('renders countdown number', () => {
    render(<CaptureOverlay durationMs={3000} onComplete={vi.fn()} />);

    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('counts down each second', () => {
    render(<CaptureOverlay durationMs={3000} onComplete={vi.fn()} />);

    expect(screen.getByText('3')).toBeInTheDocument();

    act(() => { vi.advanceTimersByTime(1000); });
    expect(screen.getByText('2')).toBeInTheDocument();

    act(() => { vi.advanceTimersByTime(1000); });
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('calls onComplete when countdown reaches zero', () => {
    const onComplete = vi.fn();
    render(<CaptureOverlay durationMs={3000} onComplete={onComplete} />);

    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });

    expect(onComplete).toHaveBeenCalledOnce();
  });
});

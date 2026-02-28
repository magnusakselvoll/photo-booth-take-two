import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, act } from '@testing-library/react';
import { CaptureOverlay } from '../CaptureOverlay';

describe('CaptureOverlay', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    // Return > 0.15 so countdown numbers are never substituted
    vi.spyOn(Math, 'random').mockReturnValue(0.5);
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.restoreAllMocks();
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

  it('shows smile phrase immediately after countdown completes', () => {
    const { container } = render(<CaptureOverlay durationMs={3000} onComplete={vi.fn()} />);

    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });

    expect(container.querySelector('.countdown')).toBeInTheDocument();
    expect(container.querySelector('.waiting-message')).not.toBeInTheDocument();
  });

  it('shows waiting message after countdown completes and 500ms elapses', () => {
    const { container } = render(<CaptureOverlay durationMs={3000} onComplete={vi.fn()} />);

    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(500); });

    expect(container.querySelector('.waiting-message')).toBeInTheDocument();
    expect(container.querySelector('.countdown')).not.toBeInTheDocument();
  });
});

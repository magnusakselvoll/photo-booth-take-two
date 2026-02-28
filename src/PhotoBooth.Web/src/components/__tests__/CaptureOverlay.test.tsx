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

  it('shows "Smile!" immediately after countdown completes', () => {
    render(<CaptureOverlay durationMs={3000} onComplete={vi.fn()} />);

    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });

    expect(screen.getByText('Smile!')).toBeInTheDocument();
  });

  it('shows waiting message after countdown completes and 500ms elapses', () => {
    render(<CaptureOverlay durationMs={3000} onComplete={vi.fn()} />);

    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(1000); });
    act(() => { vi.advanceTimersByTime(500); });

    expect(screen.getByText('Developing photo...')).toBeInTheDocument();
    expect(screen.queryByText('Smile!')).not.toBeInTheDocument();
  });
});

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, fireEvent, act } from '@testing-library/react';
import { BoothPage } from '../BoothPage';
import type { PhotoBoothEvent } from '../../api/types';

let capturedOnEvent: ((event: PhotoBoothEvent) => void) | null = null;

vi.mock('../../api/events', () => ({
  useEventStream: (handler: (event: PhotoBoothEvent) => void) => {
    capturedOnEvent = handler;
  },
}));

const mockTriggerCapture = vi.fn();

vi.mock('../../api/client', () => ({
  triggerCapture: (...args: unknown[]) => mockTriggerCapture(...(args as [])),
}));

vi.mock('../../hooks/useSlideshowNavigation', () => ({
  useSlideshowNavigation: () => ({
    currentPhoto: null,
    goNext: vi.fn(),
    goPrevious: vi.fn(),
    skip: vi.fn(),
    toggleMode: vi.fn(),
    refresh: vi.fn().mockResolvedValue(undefined),
    currentIndex: 0,
    totalPhotos: 0,
    isRandom: false,
  }),
}));

vi.mock('../../hooks/useKeyboardNavigation', () => ({
  useKeyboardNavigation: () => {},
}));

vi.mock('../../hooks/useGamepadNavigation', () => ({
  useGamepadNavigation: () => {},
}));

vi.mock('../../components/Slideshow', () => ({
  Slideshow: () => <div data-testid="slideshow" />,
}));

vi.mock('../../components/CaptureOverlay', () => ({
  CaptureOverlay: ({ durationMs }: { durationMs: number }) => (
    <div data-testid="capture-overlay" data-duration={durationMs} />
  ),
}));

vi.mock('../../components/PhotoDisplay', () => ({
  PhotoDisplay: ({ code }: { code: string }) => (
    <div data-testid="photo-display" data-code={code} />
  ),
}));

const countdownEvent = (durationMs = 3000): PhotoBoothEvent => ({
  eventType: 'countdown-started',
  durationMs,
  triggerSource: 'api',
  timestamp: '2025-01-01T00:00:00Z',
});

const photoCapturedEvent = (): PhotoBoothEvent => ({
  eventType: 'photo-captured',
  photoId: 'photo-1',
  code: '42',
  imageUrl: '/api/photos/photo-1/image',
  timestamp: '2025-01-01T00:00:00Z',
});

const captureFailedEvent = (error = 'Camera error'): PhotoBoothEvent => ({
  eventType: 'capture-failed',
  error,
  timestamp: '2025-01-01T00:00:00Z',
});

describe('BoothPage', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(Math, 'random').mockReturnValue(0.5);
    mockTriggerCapture.mockResolvedValue({ message: 'ok', countdownDurationMs: 3000 });
    capturedOnEvent = null;
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('renders slideshow when no captured photo', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    expect(screen.getByTestId('slideshow')).toBeInTheDocument();
  });

  it('does not render CaptureOverlay initially', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    expect(screen.queryByTestId('capture-overlay')).not.toBeInTheDocument();
  });

  it('calls triggerCapture when page is clicked', async () => {
    const { container } = render(<BoothPage watchdogTimeoutMs={60000} />);
    await act(async () => {
      fireEvent.click(container.querySelector('.booth-page')!);
    });
    expect(mockTriggerCapture).toHaveBeenCalledOnce();
  });

  it('shows error message when triggerCapture rejects', async () => {
    mockTriggerCapture.mockRejectedValue(new Error('Network error'));
    const { container } = render(<BoothPage watchdogTimeoutMs={60000} />);
    await act(async () => {
      fireEvent.click(container.querySelector('.booth-page')!);
    });
    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('clears error message after 3000ms', async () => {
    mockTriggerCapture.mockRejectedValue(new Error('Network error'));
    const { container } = render(<BoothPage watchdogTimeoutMs={60000} />);
    await act(async () => {
      fireEvent.click(container.querySelector('.booth-page')!);
    });
    expect(screen.getByText('Network error')).toBeInTheDocument();

    act(() => { vi.advanceTimersByTime(3000); });
    expect(screen.queryByText('Network error')).not.toBeInTheDocument();
  });

  it('shows CaptureOverlay when countdown-started event arrives', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    act(() => { capturedOnEvent!(countdownEvent()); });
    expect(screen.getByTestId('capture-overlay')).toBeInTheDocument();
  });

  it('shows PhotoDisplay when photo-captured event arrives', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    act(() => { capturedOnEvent!(countdownEvent()); });
    act(() => { capturedOnEvent!(photoCapturedEvent()); });
    expect(screen.getByTestId('photo-display')).toBeInTheDocument();
  });

  it('hides CaptureOverlay after photo is captured', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    act(() => { capturedOnEvent!(countdownEvent()); });
    expect(screen.getByTestId('capture-overlay')).toBeInTheDocument();
    act(() => { capturedOnEvent!(photoCapturedEvent()); });
    expect(screen.queryByTestId('capture-overlay')).not.toBeInTheDocument();
  });

  it('shows error message on capture-failed event', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    act(() => { capturedOnEvent!(countdownEvent()); });
    act(() => { capturedOnEvent!(captureFailedEvent('Camera error')); });
    expect(screen.getByText('Camera error')).toBeInTheDocument();
  });

  it('hides CaptureOverlay after capture fails', () => {
    render(<BoothPage watchdogTimeoutMs={60000} />);
    act(() => { capturedOnEvent!(countdownEvent()); });
    expect(screen.getByTestId('capture-overlay')).toBeInTheDocument();
    act(() => { capturedOnEvent!(captureFailedEvent()); });
    expect(screen.queryByTestId('capture-overlay')).not.toBeInTheDocument();
  });

  it('calls window.location.reload after watchdog timeout', () => {
    const reloadMock = vi.fn();
    vi.stubGlobal('location', { reload: reloadMock });
    render(<BoothPage watchdogTimeoutMs={1000} />);
    act(() => { vi.advanceTimersByTime(1000); });
    expect(reloadMock).toHaveBeenCalled();
  });
});

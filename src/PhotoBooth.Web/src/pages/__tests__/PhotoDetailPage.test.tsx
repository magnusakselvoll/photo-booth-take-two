import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import React from 'react';
import { render, screen, cleanup, fireEvent, waitFor, act } from '@testing-library/react';
import { PhotoDetailPage } from '../PhotoDetailPage';
import type { PhotoDto } from '../../api/types';

const mockNavigate = vi.fn();
const mockUseParams = vi.fn();

vi.mock('react-router-dom', () => ({
  useParams: () => mockUseParams(),
  useNavigate: () => mockNavigate,
}));

const mockGetPhotoByCode = vi.fn<(code: string) => Promise<PhotoDto | null>>();
const mockGetAllPhotos = vi.fn<() => Promise<PhotoDto[]>>();

vi.mock('../../api/client', () => ({
  getPhotoByCode: (code: string) => mockGetPhotoByCode(code),
  getPhotoImageUrl: (photoId: string, width?: number) => {
    const base = `/api/photos/${photoId}/image`;
    return width !== undefined ? `${base}?width=${width}` : base;
  },
  getAllPhotos: () => mockGetAllPhotos(),
}));

const swipeConfigSpy = vi.fn();
vi.mock('../../hooks/useSwipeNavigation', () => ({
  useSwipeNavigation: (config: unknown) => swipeConfigSpy(config),
}));

type OnTransformFn = (ref: unknown, state: { scale: number }) => void;
let capturedOnTransform: OnTransformFn | undefined;

vi.mock('react-zoom-pan-pinch', () => ({
  TransformWrapper: ({ children, onTransform }: { children: React.ReactNode; onTransform?: OnTransformFn }) => {
    capturedOnTransform = onTransform;
    return <>{children}</>;
  },
  TransformComponent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('../../i18n/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        loading: 'Loading...',
        photoNotFoundError: 'Photo not found',
        backToGallery: 'Back to Gallery',
        getPhoto: 'Get Photo',
        downloadPhoto: 'Download Photo',
        sharePhoto: 'Share Photo',
        previousPhoto: 'Previous photo',
        nextPhoto: 'Next photo',
      };
      return map[key] ?? key;
    },
  }),
}));

const mockPhoto: PhotoDto = {
  id: 'photo-abc',
  code: '42',
  capturedAt: '2025-01-01T00:00:00Z',
};

const mockPrevPhoto: PhotoDto = {
  id: 'photo-prev',
  code: '41',
  capturedAt: '2025-01-01T00:00:00Z',
};

const mockNextPhoto: PhotoDto = {
  id: 'photo-next',
  code: '43',
  capturedAt: '2025-01-01T00:00:00Z',
};

describe('PhotoDetailPage', () => {
  beforeEach(() => {
    mockUseParams.mockReturnValue({ code: '42' });
    mockNavigate.mockClear();
    swipeConfigSpy.mockClear();
    capturedOnTransform = undefined;
    mockGetPhotoByCode.mockResolvedValue(mockPhoto);
    mockGetAllPhotos.mockResolvedValue([mockPhoto]);
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows loading indicator while photo is being fetched', () => {
    mockGetPhotoByCode.mockReturnValue(new Promise(() => {}));
    mockGetAllPhotos.mockReturnValue(new Promise(() => {}));
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('shows error when no code parameter is provided', () => {
    mockUseParams.mockReturnValue({ code: undefined });
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    expect(screen.getByText('Photo not found')).toBeInTheDocument();
  });

  it('shows error when photo not found', async () => {
    mockGetPhotoByCode.mockResolvedValue(null);
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => {
      expect(screen.getByText('Photo not found')).toBeInTheDocument();
    });
  });

  it('renders photo image with correct src on success', async () => {
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => {
      const img = screen.getByRole('img');
      expect(img).toHaveAttribute('src', '/api/photos/photo-abc/image?width=1200');
    });
  });

  it('displays the photo code in nav bar', async () => {
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => {
      const codeEl = document.querySelector('.photo-detail-code');
      expect(codeEl).toHaveTextContent('42');
    });
  });

  it('navigates to /download when back button is clicked', async () => {
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'Back to Gallery' }));
    expect(mockNavigate).toHaveBeenCalledWith('/testprefix/download');
  });

  it('shows speed dial trigger button', async () => {
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Get Photo' })).toBeInTheDocument();
    });
  });

  it('expands speed dial after fetching blob on trigger click', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      blob: () => Promise.resolve(new Blob(['image data'], { type: 'image/jpeg' })),
    } as Response));

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Get Photo' }));
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Download Photo' })).toBeInTheDocument();
    });
  });

  it('shows download button when expanded', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      blob: () => Promise.resolve(new Blob(['image data'], { type: 'image/jpeg' })),
    } as Response));

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Get Photo' }));
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Download Photo' })).toBeInTheDocument();
    });
  });

  it('shows share button when navigator.canShare is true', async () => {
    Object.defineProperty(navigator, 'canShare', {
      value: () => true,
      configurable: true,
      writable: true,
    });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      blob: () => Promise.resolve(new Blob(['image data'], { type: 'image/jpeg' })),
    } as Response));

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Get Photo' }));
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Share Photo' })).toBeInTheDocument();
    });
  });

  it('passes disabled: false to useSwipeNavigation on initial render', async () => {
    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());
    const lastCall = swipeConfigSpy.mock.calls[swipeConfigSpy.mock.calls.length - 1][0];
    expect(lastCall.disabled).toBe(false);
  });

  it('hides share button when navigator.canShare is false', async () => {
    Object.defineProperty(navigator, 'canShare', {
      value: undefined,
      configurable: true,
      writable: true,
    });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      blob: () => Promise.resolve(new Blob(['image data'], { type: 'image/jpeg' })),
    } as Response));

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Get Photo' }));
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Download Photo' })).toBeInTheDocument();
    });
    expect(screen.queryByRole('button', { name: 'Share Photo' })).not.toBeInTheDocument();
  });

  it('shows loading state when clicking next arrow (goToPhoto resets state before navigation)', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPrevPhoto, mockPhoto, mockNextPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    // clicking next calls goToPhoto which synchronously sets loading=true and photo=null
    // before calling navigate (which is a spy here and won't change the route)
    fireEvent.click(screen.getByRole('button', { name: 'Next photo' }));

    // after the click, loading state should be shown because photo was cleared
    await waitFor(() => expect(screen.getByText('Loading...')).toBeInTheDocument());
  });

  it('renders prev and next nav arrow buttons when neighbours exist', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPrevPhoto, mockPhoto, mockNextPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    expect(screen.getByRole('button', { name: 'Previous photo' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next photo' })).toBeInTheDocument();
  });

  it('clicking next arrow navigates to the next photo', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPrevPhoto, mockPhoto, mockNextPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Next photo' }));
    expect(mockNavigate).toHaveBeenCalledWith('/testprefix/photo/43');
  });

  it('clicking prev arrow navigates to the previous photo', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPrevPhoto, mockPhoto, mockNextPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Previous photo' }));
    expect(mockNavigate).toHaveBeenCalledWith('/testprefix/photo/41');
  });

  it('hides prev arrow when at the first photo', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPhoto, mockNextPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    expect(screen.queryByRole('button', { name: 'Previous photo' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next photo' })).toBeInTheDocument();
  });

  it('hides next arrow when at the last photo', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPrevPhoto, mockPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    expect(screen.getByRole('button', { name: 'Previous photo' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Next photo' })).not.toBeInTheDocument();
  });

  it('hides nav arrows when zoomed', async () => {
    mockGetAllPhotos.mockResolvedValue([mockPrevPhoto, mockPhoto, mockNextPhoto]);

    render(<PhotoDetailPage urlPrefix="testprefix" />);
    await waitFor(() => expect(screen.queryByText('Loading...')).not.toBeInTheDocument());

    // arrows visible before zoom
    expect(screen.getByRole('button', { name: 'Previous photo' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next photo' })).toBeInTheDocument();

    // trigger zoom via the onTransform callback captured by the TransformWrapper mock
    await act(async () => { capturedOnTransform?.(null, { scale: 2 }); });

    expect(screen.queryByRole('button', { name: 'Previous photo' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Next photo' })).not.toBeInTheDocument();
  });
});

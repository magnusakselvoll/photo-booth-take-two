import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { PhotoGrid } from '../PhotoGrid';
import { getGalleryCache, setGalleryCache, __resetForTests } from '../galleryCache';
import type { PhotoDto } from '../../api/types';

const mockGetPhotosPage = vi.fn<(pageSize: number, cursor?: string) => Promise<{ photos: PhotoDto[]; nextCursor: string | null }>>();

vi.mock('../../api/client', () => ({
  getPhotosPage: (pageSize: number, cursor?: string) => mockGetPhotosPage(pageSize, cursor),
  getPhotoImageUrl: (photoId: string, width?: number) => {
    const base = `/api/photos/${photoId}/image`;
    return width !== undefined ? `${base}?width=${width}` : base;
  },
}));

vi.mock('../../i18n/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        loadingPhotos: 'Loading photos...',
        failedToLoadPhotos: 'Failed to load photos',
        noPhotosYet: 'No photos yet',
      };
      return map[key] ?? key;
    },
  }),
}));

class MockIntersectionObserver {
  observe = vi.fn();
  disconnect = vi.fn();
  unobserve = vi.fn();
  constructor(_callback: IntersectionObserverCallback, _options?: IntersectionObserverInit) {}
}

const samplePhotos: PhotoDto[] = [
  { id: 'id-1', code: '1', capturedAt: '2024-01-01T00:00:00Z' },
  { id: 'id-2', code: '2', capturedAt: '2024-01-02T00:00:00Z' },
];

describe('PhotoGrid', () => {
  beforeEach(() => {
    __resetForTests();
    mockGetPhotosPage.mockClear();
    vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined);
    vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('fetches and renders photos on first mount when cache is empty', async () => {
    mockGetPhotosPage.mockResolvedValue({ photos: samplePhotos, nextCursor: null });

    render(<PhotoGrid onPhotoClick={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getAllByRole('img')).toHaveLength(2);
    });

    expect(mockGetPhotosPage).toHaveBeenCalledOnce();
  });

  it('renders cached photos without fetching when cache is populated', () => {
    setGalleryCache(samplePhotos, null, 0);

    render(<PhotoGrid onPhotoClick={vi.fn()} />);

    expect(screen.getAllByRole('img')).toHaveLength(2);
    expect(mockGetPhotosPage).not.toHaveBeenCalled();
  });

  it('saves photos, nextCursor, and scrollY to cache on unmount', async () => {
    mockGetPhotosPage.mockResolvedValue({ photos: samplePhotos, nextCursor: 'cursor-abc' });

    const { unmount } = render(<PhotoGrid onPhotoClick={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getAllByRole('img')).toHaveLength(2);
    });

    unmount();

    const cache = getGalleryCache();
    expect(cache.photos).toEqual(samplePhotos);
    expect(cache.nextCursor).toBe('cursor-abc');
    expect(typeof cache.scrollTop).toBe('number');
  });

  it('restores scroll position from cache on hydrated mount', () => {
    setGalleryCache(samplePhotos, null, 500);

    render(<PhotoGrid onPhotoClick={vi.fn()} />);

    expect(window.scrollTo).toHaveBeenCalledWith(0, 500);
  });

  it('does not restore scroll when cache is empty', async () => {
    mockGetPhotosPage.mockResolvedValue({ photos: samplePhotos, nextCursor: null });

    render(<PhotoGrid onPhotoClick={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getAllByRole('img')).toHaveLength(2);
    });

    expect(window.scrollTo).not.toHaveBeenCalled();
  });

  it('calls onPhotoClick with the photo code when a tile is clicked', async () => {
    mockGetPhotosPage.mockResolvedValue({ photos: samplePhotos, nextCursor: null });
    const onPhotoClick = vi.fn();

    render(<PhotoGrid onPhotoClick={onPhotoClick} />);

    await waitFor(() => {
      expect(screen.getAllByRole('img')).toHaveLength(2);
    });

    screen.getAllByRole('img')[0].closest('.photo-grid-item')!.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );

    expect(onPhotoClick).toHaveBeenCalledWith('1');
  });
});

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  triggerCapture,
  getPhotoByCode,
  getPhotoImageUrl,
  getNextSlideshowPhoto,
  getClientConfig,
  getAllPhotos,
} from '../client';

describe('API client', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn<(input: string | URL | Request, init?: RequestInit) => Promise<Response>>(),
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function mockFetch(status: number, body?: unknown): void {
    vi.mocked(fetch).mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      statusText: status === 200 ? 'OK' : 'Error',
      json: () => Promise.resolve(body),
    } as Response);
  }

  describe('triggerCapture', () => {
    it('sends POST to /api/photos/trigger', async () => {
      mockFetch(200, { message: 'ok', countdownDurationMs: 3000 });

      await triggerCapture();

      expect(fetch).toHaveBeenCalledWith('/api/photos/trigger', { method: 'POST' });
    });

    it('includes durationMs query param when provided', async () => {
      mockFetch(200, { message: 'ok', countdownDurationMs: 5000 });

      await triggerCapture(5000);

      expect(fetch).toHaveBeenCalledWith('/api/photos/trigger?durationMs=5000', {
        method: 'POST',
      });
    });

    it('throws on non-ok response', async () => {
      mockFetch(429);

      await expect(triggerCapture()).rejects.toThrow('Failed to trigger capture');
    });
  });

  describe('getPhotoByCode', () => {
    it('sends GET to /api/photos/:code', async () => {
      const photo = { id: 'abc', code: '123', capturedAt: '2025-01-01' };
      mockFetch(200, photo);

      const result = await getPhotoByCode('123');

      expect(fetch).toHaveBeenCalledWith('/api/photos/123');
      expect(result).toEqual(photo);
    });

    it('returns null on 404', async () => {
      mockFetch(404);

      const result = await getPhotoByCode('999');

      expect(result).toBeNull();
    });

    it('throws on other non-ok responses', async () => {
      mockFetch(500);

      await expect(getPhotoByCode('123')).rejects.toThrow('Failed to get photo');
    });
  });

  describe('getPhotoImageUrl', () => {
    it('builds correct URL string without making a fetch call', () => {
      const url = getPhotoImageUrl('photo-abc');

      expect(url).toBe('/api/photos/photo-abc/image');
      expect(fetch).not.toHaveBeenCalled();
    });
  });

  describe('getNextSlideshowPhoto', () => {
    it('sends GET to /api/slideshow/next', async () => {
      const photo = { id: 'abc', code: '1', capturedAt: '2025-01-01', imageUrl: '/img' };
      mockFetch(200, photo);

      const result = await getNextSlideshowPhoto();

      expect(fetch).toHaveBeenCalledWith('/api/slideshow/next');
      expect(result).toEqual(photo);
    });

    it('returns null on 404', async () => {
      mockFetch(404);

      const result = await getNextSlideshowPhoto();

      expect(result).toBeNull();
    });

    it('throws on non-ok response', async () => {
      mockFetch(500);

      await expect(getNextSlideshowPhoto()).rejects.toThrow('Failed to get slideshow photo');
    });
  });

  describe('getClientConfig', () => {
    it('sends GET to /api/config', async () => {
      const config = { qrCodeBaseUrl: null, swirlEffect: true };
      mockFetch(200, config);

      const result = await getClientConfig();

      expect(fetch).toHaveBeenCalledWith('/api/config');
      expect(result).toEqual(config);
    });

    it('throws on non-ok response', async () => {
      mockFetch(500);

      await expect(getClientConfig()).rejects.toThrow('Failed to get config');
    });
  });

  describe('getAllPhotos', () => {
    it('sends GET to /api/photos', async () => {
      const photos = [{ id: '1', code: '001', capturedAt: '2025-01-01' }];
      mockFetch(200, photos);

      const result = await getAllPhotos();

      expect(fetch).toHaveBeenCalledWith('/api/photos');
      expect(result).toEqual(photos);
    });

    it('throws on non-ok response', async () => {
      mockFetch(500);

      await expect(getAllPhotos()).rejects.toThrow('Failed to get photos');
    });
  });
});

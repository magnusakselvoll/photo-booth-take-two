import type { PhotoDto } from '../api/types';

interface GalleryCache {
  photos: PhotoDto[] | null;
  nextCursor: string | null | undefined;
  scrollTop: number;
}

const cache: GalleryCache = {
  photos: null,
  nextCursor: undefined,
  scrollTop: 0,
};

export function getGalleryCache(): Readonly<GalleryCache> {
  return cache;
}

export function setGalleryCache(photos: PhotoDto[], nextCursor: string | null | undefined, scrollTop: number): void {
  cache.photos = photos;
  cache.nextCursor = nextCursor;
  cache.scrollTop = scrollTop;
}

export function hasGalleryCache(): boolean {
  return cache.photos !== null && cache.photos.length > 0;
}

export function clearGalleryCache(): void {
  cache.photos = null;
  cache.nextCursor = undefined;
  cache.scrollTop = 0;
}

export function __resetForTests(): void {
  clearGalleryCache();
}

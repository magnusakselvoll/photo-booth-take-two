import type { PhotoDto, SlideshowPhotoDto, TriggerResponse } from './types';

const API_BASE = '/api';

export async function triggerCapture(): Promise<TriggerResponse> {
  const response = await fetch(`${API_BASE}/photos/trigger`, {
    method: 'POST',
  });

  if (!response.ok) {
    throw new Error(`Failed to trigger capture: ${response.statusText}`);
  }

  return response.json();
}

export async function getPhotoByCode(code: string): Promise<PhotoDto | null> {
  const response = await fetch(`${API_BASE}/photos/${encodeURIComponent(code)}`);

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to get photo: ${response.statusText}`);
  }

  return response.json();
}

export function getPhotoImageUrl(photoId: string): string {
  return `${API_BASE}/photos/${photoId}/image`;
}

export async function getNextSlideshowPhoto(): Promise<SlideshowPhotoDto | null> {
  const response = await fetch(`${API_BASE}/slideshow/next`);

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to get slideshow photo: ${response.statusText}`);
  }

  return response.json();
}

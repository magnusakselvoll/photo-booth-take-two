import type { ClientConfigDto, PhotoDto, SlideshowPhotoDto, TriggerResponse } from './types';

const API_BASE = '/api';

export async function triggerCapture(durationMs?: number): Promise<TriggerResponse> {
  const url = durationMs
    ? `${API_BASE}/photos/trigger?durationMs=${durationMs}`
    : `${API_BASE}/photos/trigger`;

  const response = await fetch(url, {
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

export async function getClientConfig(): Promise<ClientConfigDto> {
  const response = await fetch(`${API_BASE}/config`);

  if (!response.ok) {
    throw new Error(`Failed to get config: ${response.statusText}`);
  }

  return response.json();
}

export async function getAllPhotos(): Promise<PhotoDto[]> {
  const response = await fetch(`${API_BASE}/photos`);

  if (!response.ok) {
    throw new Error(`Failed to get photos: ${response.statusText}`);
  }

  return response.json();
}

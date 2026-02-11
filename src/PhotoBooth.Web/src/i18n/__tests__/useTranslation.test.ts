import { describe, it, expect, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useTranslation } from '../useTranslation';

describe('useTranslation', () => {
  afterEach(() => {
    // Reset URL to remove any ?lang= params
    window.history.replaceState({}, '', '/');
  });

  it('returns English translations by default', () => {
    // navigator.language defaults to 'en' in jsdom
    const { result } = renderHook(() => useTranslation());

    expect(result.current.language).toBe('en');
    expect(result.current.t('findPhoto')).toBe('Find Photo');
  });

  it('respects ?lang=es URL parameter', () => {
    window.history.replaceState({}, '', '/?lang=es');

    const { result } = renderHook(() => useTranslation());

    expect(result.current.language).toBe('es');
    expect(result.current.t('findPhoto')).toBe('Buscar Foto');
  });

  it('respects ?lang=en URL parameter', () => {
    window.history.replaceState({}, '', '/?lang=en');

    const { result } = renderHook(() => useTranslation());

    expect(result.current.language).toBe('en');
    expect(result.current.t('findPhoto')).toBe('Find Photo');
  });

  it('falls back to English for unknown lang parameter', () => {
    window.history.replaceState({}, '', '/?lang=fr');

    const { result } = renderHook(() => useTranslation());

    // Unknown language falls through to getDeviceLanguage()
    expect(result.current.language).toBe('en');
  });

  it('t() returns correct string for each key', () => {
    const { result } = renderHook(() => useTranslation());

    expect(result.current.t('downloadYourPhoto')).toBe('Download Your Photo');
    expect(result.current.t('searching')).toBe('Searching...');
    expect(result.current.t('tapToTakePhoto')).toBe('Tap anywhere to take a photo');
  });

  it('setLanguage changes the language and updates URL', () => {
    const { result } = renderHook(() => useTranslation());

    act(() => result.current.setLanguage('es'));

    expect(result.current.language).toBe('es');
    expect(result.current.t('findPhoto')).toBe('Buscar Foto');
    expect(window.location.search).toContain('lang=es');
  });
});

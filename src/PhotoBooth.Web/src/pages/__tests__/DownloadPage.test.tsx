import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { DownloadPage } from '../DownloadPage';

const mockNavigate = vi.fn();

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

vi.mock('../../components/PhotoGrid', () => ({
  PhotoGrid: ({ onPhotoClick }: { onPhotoClick: (code: string) => void }) => (
    <div data-testid="photo-grid" onClick={() => onPhotoClick('42')} />
  ),
}));

describe('DownloadPage', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
    // Reset URL so language state doesn't leak between tests
    window.history.replaceState({}, '', '/');
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders search input with placeholder text', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    expect(screen.getByPlaceholderText('Photo code')).toBeInTheDocument();
  });

  it('submit button is disabled when input is empty', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    expect(screen.getByRole('button', { name: 'Find Photo' })).toBeDisabled();
  });

  it('enables submit button when code is entered', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    fireEvent.change(screen.getByPlaceholderText('Photo code'), { target: { value: '42' } });
    expect(screen.getByRole('button', { name: 'Find Photo' })).not.toBeDisabled();
  });

  it('navigates to /{prefix}/photo/{code} on form submit', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    const input = screen.getByPlaceholderText('Photo code');
    fireEvent.change(input, { target: { value: '42' } });
    fireEvent.submit(input.closest('form')!);
    expect(mockNavigate).toHaveBeenCalledWith('/testprefix/photo/42');
  });

  it('trims whitespace from code before navigating', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    const input = screen.getByPlaceholderText('Photo code');
    fireEvent.change(input, { target: { value: '  42  ' } });
    fireEvent.submit(input.closest('form')!);
    expect(mockNavigate).toHaveBeenCalledWith('/testprefix/photo/42');
  });

  it('does not navigate when code is only whitespace', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    const input = screen.getByPlaceholderText('Photo code');
    fireEvent.change(input, { target: { value: '   ' } });
    fireEvent.submit(input.closest('form')!);
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('renders PhotoGrid component', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    expect(screen.getByTestId('photo-grid')).toBeInTheDocument();
  });

  it('navigates to /{prefix}/photo/{code} when PhotoGrid onPhotoClick fires', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    fireEvent.click(screen.getByTestId('photo-grid'));
    expect(mockNavigate).toHaveBeenCalledWith('/testprefix/photo/42');
  });

  it('language toggle switches active class', () => {
    render(<DownloadPage urlPrefix="testprefix" />);
    const englishBtn = screen.getByRole('button', { name: 'English' });
    const spanishBtn = screen.getByRole('button', { name: 'Español' });

    // English should be active initially (jsdom navigator.language defaults to 'en')
    expect(englishBtn).toHaveClass('active');
    expect(spanishBtn).not.toHaveClass('active');

    fireEvent.click(spanishBtn);
    expect(spanishBtn).toHaveClass('active');
    expect(englishBtn).not.toHaveClass('active');
  });
});

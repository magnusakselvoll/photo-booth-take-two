import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { NotFoundPage } from '../NotFoundPage';

const mockNavigate = vi.fn();

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

describe('NotFoundPage', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
    window.history.replaceState({}, '', '/');
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders 404 text', () => {
    render(<NotFoundPage />);
    expect(screen.getByText('404')).toBeInTheDocument();
  });

  it('renders page not found message', () => {
    render(<NotFoundPage />);
    expect(screen.getByText('Page not found')).toBeInTheDocument();
  });

  it('renders a button to go to the gallery', () => {
    render(<NotFoundPage />);
    expect(screen.getByRole('button', { name: 'Go to Gallery' })).toBeInTheDocument();
  });

  it('navigates to /download when gallery button is clicked', () => {
    render(<NotFoundPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Go to Gallery' }));
    expect(mockNavigate).toHaveBeenCalledWith('/download');
  });
});

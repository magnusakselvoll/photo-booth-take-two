import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';
import { PhotoDisplay } from '../PhotoDisplay';

describe('PhotoDisplay', () => {
  afterEach(cleanup);

  it('renders image with correct src', () => {
    const { getByRole } = render(<PhotoDisplay photoId="photo-abc" code="42" />);

    const img = getByRole('img');
    expect(img).toHaveAttribute('src', '/api/photos/photo-abc/image');
  });

  it('renders image with correct alt text', () => {
    const { getByRole } = render(<PhotoDisplay photoId="photo-abc" code="42" />);

    const img = getByRole('img');
    expect(img).toHaveAttribute('alt', 'Photo 42');
  });

  it('shows code overlay by default', () => {
    const { container } = render(<PhotoDisplay photoId="photo-abc" code="42" />);

    const codeEl = container.querySelector('.photo-code');
    expect(codeEl).toBeInTheDocument();
    expect(codeEl).toHaveTextContent('42');
  });

  it('hides code overlay when showCode is false', () => {
    const { container } = render(<PhotoDisplay photoId="photo-abc" code="42" showCode={false} />);

    const codeOverlay = container.querySelector('.photo-code-overlay');
    expect(codeOverlay).not.toBeInTheDocument();
  });

  it('shows QR code by default', () => {
    const { container } = render(<PhotoDisplay photoId="photo-abc" code="42" />);

    const qrContainer = container.querySelector('.qr-code-container');
    expect(qrContainer).toBeInTheDocument();
  });

  it('hides QR code when showQrCode is false', () => {
    const { container } = render(
      <PhotoDisplay photoId="photo-abc" code="42" showQrCode={false} />,
    );

    const qrContainer = container.querySelector('.qr-code-container');
    expect(qrContainer).not.toBeInTheDocument();
  });

  it('hides code overlay when fadingOut is true', () => {
    const { container } = render(<PhotoDisplay photoId="photo-abc" code="42" fadingOut={true} />);

    const codeOverlay = container.querySelector('.photo-code-overlay');
    expect(codeOverlay).not.toBeInTheDocument();
  });

  it('applies swirl class by default', () => {
    const { container } = render(<PhotoDisplay photoId="photo-abc" code="42" />);

    const display = container.querySelector('.photo-display');
    expect(display).toHaveClass('swirl');
  });

  it('applies fade class when swirlEffect is false', () => {
    const { container } = render(
      <PhotoDisplay photoId="photo-abc" code="42" swirlEffect={false} />,
    );

    const display = container.querySelector('.photo-display');
    expect(display).toHaveClass('fade');
    expect(display).not.toHaveClass('swirl');
  });
});

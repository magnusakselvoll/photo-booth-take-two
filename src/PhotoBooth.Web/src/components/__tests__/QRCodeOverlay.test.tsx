import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';
import { QRCodeOverlay } from '../QRCodeOverlay';

describe('QRCodeOverlay', () => {
  afterEach(cleanup);

  it('renders a QR code container', () => {
    const { container } = render(
      <QRCodeOverlay code="123" baseUrl="https://example.com" />,
    );

    const qrContainer = container.querySelector('.qr-code-container');
    expect(qrContainer).toBeInTheDocument();
  });

  it('renders an SVG QR code', () => {
    const { container } = render(
      <QRCodeOverlay code="42" baseUrl="https://booth.local" />,
    );

    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });
});

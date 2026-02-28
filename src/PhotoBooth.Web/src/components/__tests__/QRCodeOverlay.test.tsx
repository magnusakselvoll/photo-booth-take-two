import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';
import { QRCodeOverlay } from '../QRCodeOverlay';

vi.mock('react-qr-code', () => ({
  default: ({ value }: { value: string }) => <div data-testid="qr-code" data-value={value} />,
}));

describe('QRCodeOverlay', () => {
  afterEach(cleanup);

  it('renders a QR code container', () => {
    const { container } = render(
      <QRCodeOverlay code="123" baseUrl="https://example.com" />,
    );

    const qrContainer = container.querySelector('.qr-code-container');
    expect(qrContainer).toBeInTheDocument();
  });

  it('encodes URL as /photo/{code}', () => {
    const { getByTestId } = render(
      <QRCodeOverlay code="42" baseUrl="https://example.com" />,
    );

    expect(getByTestId('qr-code').getAttribute('data-value')).toBe(
      'https://example.com/photo/42',
    );
  });

  it('strips trailing slash from baseUrl', () => {
    const { getByTestId } = render(
      <QRCodeOverlay code="7" baseUrl="https://example.com/" />,
    );

    expect(getByTestId('qr-code').getAttribute('data-value')).toBe(
      'https://example.com/photo/7',
    );
  });

  it('works without trailing slash on baseUrl', () => {
    const { getByTestId } = render(
      <QRCodeOverlay code="1" baseUrl="http://booth.local:5000" />,
    );

    expect(getByTestId('qr-code').getAttribute('data-value')).toBe(
      'http://booth.local:5000/photo/1',
    );
  });
});

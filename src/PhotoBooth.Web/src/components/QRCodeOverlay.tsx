import _QRCodeDefault from 'react-qr-code';

// react-qr-code is a legacy CJS package. Vite 8 exposes the CJS exports namespace as the
// default import rather than exports.default, so the component lives at .QRCode on that object.
// The ?? fallback handles the mock in tests (where default is the function directly).
const QRCode = ((_QRCodeDefault as unknown as { QRCode: typeof _QRCodeDefault }).QRCode)
  ?? _QRCodeDefault;

interface QRCodeOverlayProps {
  code: string;
  baseUrl: string;
  urlPrefix: string;
}

export function QRCodeOverlay({ code, baseUrl, urlPrefix }: QRCodeOverlayProps) {
  const downloadUrl = `${baseUrl.replace(/\/$/, '')}/${urlPrefix}/photo/${code}`;

  return (
    <div className="qr-code-container">
      <QRCode
        value={downloadUrl}
        size={128}
        bgColor="#ffffff"
        fgColor="#000000"
        level="M"
      />
    </div>
  );
}

import QRCode from 'react-qr-code';

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

import { useState } from 'react';
import { getPhotoByCode, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';

export function DownloadPage() {
  const [code, setCode] = useState('');
  const [photo, setPhoto] = useState<PhotoDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!code.trim()) return;

    setLoading(true);
    setError(null);

    try {
      const result = await getPhotoByCode(code.trim());
      if (result) {
        setPhoto(result);
      } else {
        setError('Photo not found. Please check your code.');
        setPhoto(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to find photo');
      setPhoto(null);
    } finally {
      setLoading(false);
    }
  };

  const handleDownload = () => {
    if (!photo) return;

    const link = document.createElement('a');
    link.href = getPhotoImageUrl(photo.id);
    link.download = `photo-${photo.code}.jpg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="download-page">
      <h1>Download Your Photo</h1>

      <form onSubmit={handleSubmit} className="code-form">
        <input
          type="text"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder="Enter your photo code"
          className="code-input"
          maxLength={10}
          autoFocus
        />
        <button type="submit" disabled={loading || !code.trim()} className="submit-button">
          {loading ? 'Searching...' : 'Find Photo'}
        </button>
      </form>

      {error && <div className="error-message">{error}</div>}

      {photo && (
        <div className="photo-result">
          <img src={getPhotoImageUrl(photo.id)} alt={`Photo ${photo.code}`} className="photo-preview" />
          <button onClick={handleDownload} className="download-button">
            Download Photo
          </button>
        </div>
      )}
    </div>
  );
}

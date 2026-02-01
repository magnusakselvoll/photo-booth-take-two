import { useState, useEffect, useCallback } from 'react';
import { getPhotoByCode, getPhotoImageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';
import { PhotoGrid } from '../components/PhotoGrid';

function getCodeFromHash(): string | null {
  const hash = window.location.hash;
  const queryStart = hash.indexOf('?');
  if (queryStart === -1) return null;

  const queryString = hash.slice(queryStart + 1);
  const params = new URLSearchParams(queryString);
  return params.get('code');
}

export function DownloadPage() {
  const [code, setCode] = useState('');
  const [photo, setPhoto] = useState<PhotoDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchPhoto = useCallback(async (photoCode: string) => {
    if (!photoCode.trim()) return;

    setLoading(true);
    setError(null);

    try {
      const result = await getPhotoByCode(photoCode.trim());
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
  }, []);

  useEffect(() => {
    const codeFromUrl = getCodeFromHash();
    if (codeFromUrl) {
      setCode(codeFromUrl);
      fetchPhoto(codeFromUrl);
    }
  }, [fetchPhoto]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await fetchPhoto(code);
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

  const handlePhotoClick = (photoCode: string) => {
    window.location.hash = `#photo/${photoCode}`;
  };

  const handleBackToSearch = () => {
    setPhoto(null);
    setCode('');
    setError(null);
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
          <div className="photo-result-actions">
            <button onClick={handleDownload} className="download-button">
              Download Photo
            </button>
            <button onClick={handleBackToSearch} className="back-button">
              Back to Search
            </button>
          </div>
        </div>
      )}

      {!photo && (
        <>
          <div className="divider">
            <span>or browse all photos</span>
          </div>
          <PhotoGrid onPhotoClick={handlePhotoClick} />
        </>
      )}
    </div>
  );
}

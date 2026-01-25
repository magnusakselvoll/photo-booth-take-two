import { getPhotoImageUrl } from '../api/client';

interface PhotoDisplayProps {
  photoId: string;
  code: string;
  showCode?: boolean;
}

export function PhotoDisplay({ photoId, code, showCode = true }: PhotoDisplayProps) {
  const imageUrl = getPhotoImageUrl(photoId);

  return (
    <div className="photo-display">
      <img src={imageUrl} alt={`Photo ${code}`} className="photo-image" />
      {showCode && <div className="photo-code">{code}</div>}
    </div>
  );
}

import type { CSSProperties } from 'react';
import { getPhotoImageUrl } from '../api/client';

export interface KenBurnsConfig {
  scaleFrom: number;
  scaleTo: number;
  xFrom: string;
  yFrom: string;
  xTo: string;
  yTo: string;
  duration: string;
}

interface PhotoDisplayProps {
  photoId: string;
  code: string;
  showCode?: boolean;
  kenBurns?: KenBurnsConfig;
  fadingOut?: boolean;
}

export function PhotoDisplay({ photoId, code, showCode = true, kenBurns, fadingOut = false }: PhotoDisplayProps) {
  const imageUrl = getPhotoImageUrl(photoId);

  const imageStyle: CSSProperties | undefined = kenBurns ? {
    '--kb-scale-from': kenBurns.scaleFrom,
    '--kb-scale-to': kenBurns.scaleTo,
    '--kb-x-from': kenBurns.xFrom,
    '--kb-y-from': kenBurns.yFrom,
    '--kb-x-to': kenBurns.xTo,
    '--kb-y-to': kenBurns.yTo,
    '--kb-duration': kenBurns.duration,
  } as CSSProperties : undefined;

  const displayClassName = fadingOut ? 'photo-display fading-out' : 'photo-display';

  return (
    <div className={displayClassName}>
      <img
        src={imageUrl}
        alt={`Photo ${code}`}
        className={kenBurns ? 'photo-image ken-burns' : 'photo-image'}
        style={imageStyle}
      />
      {showCode && !fadingOut && <div className="photo-code">{code}</div>}
    </div>
  );
}

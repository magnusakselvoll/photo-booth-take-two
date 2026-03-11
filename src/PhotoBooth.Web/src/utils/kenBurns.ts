import type { KenBurnsConfig } from '../components/PhotoDisplay';

function randomInRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

export function generateKenBurnsConfig(intervalMs: number): KenBurnsConfig {
  // Random zoom direction: in or out
  const zoomIn = Math.random() > 0.5;

  // Stronger scale range: 1.08-1.12 to 1.18-1.28
  const scaleSmall = randomInRange(1.08, 1.12);
  const scaleLarge = randomInRange(1.18, 1.28);

  // Random pan direction with randomized amount (3-6%)
  const panAmount = randomInRange(3, 6);
  const panDirections = [
    { x: panAmount, y: 0 },      // right
    { x: -panAmount, y: 0 },     // left
    { x: 0, y: panAmount },      // down
    { x: 0, y: -panAmount },     // up
    { x: panAmount * 0.7, y: panAmount * 0.7 },   // diagonal down-right
    { x: -panAmount * 0.7, y: panAmount * 0.7 },  // diagonal down-left
    { x: panAmount * 0.7, y: -panAmount * 0.7 },  // diagonal up-right
    { x: -panAmount * 0.7, y: -panAmount * 0.7 }, // diagonal up-left
  ];
  const pan = panDirections[Math.floor(Math.random() * panDirections.length)];

  // Duration slightly overshoots the slideshow interval to avoid freezing at the end
  const duration = intervalMs / 1000;

  if (zoomIn) {
    return {
      scaleFrom: scaleSmall,
      scaleTo: scaleLarge,
      xFrom: '0%',
      yFrom: '0%',
      xTo: `${pan.x}%`,
      yTo: `${pan.y}%`,
      duration: `${duration.toFixed(1)}s`,
    };
  } else {
    return {
      scaleFrom: scaleLarge,
      scaleTo: scaleSmall,
      xFrom: '0%',
      yFrom: '0%',
      xTo: `${pan.x}%`,
      yTo: `${pan.y}%`,
      duration: `${duration.toFixed(1)}s`,
    };
  }
}

export interface PhotoDto {
  id: string;
  code: string;
  capturedAt: string;
}

export interface SlideshowPhotoDto {
  id: string;
  code: string;
  capturedAt: string;
  imageUrl: string;
}

export interface CaptureResultDto {
  id: string;
  code: string;
  capturedAt: string;
}

export interface CameraInfoDto {
  isAvailable: boolean;
  captureLatencyMs: number;
}

export interface TriggerResponse {
  message: string;
  countdownDurationMs: number;
}

export interface CountdownStartedEvent {
  eventType: 'countdown-started';
  durationMs: number;
  triggerSource: string;
  timestamp: string;
}

export interface PhotoCapturedEvent {
  eventType: 'photo-captured';
  photoId: string;
  code: string;
  imageUrl: string;
  timestamp: string;
}

export interface CaptureFailedEvent {
  eventType: 'capture-failed';
  error: string;
  timestamp: string;
}

export type PhotoBoothEvent = CountdownStartedEvent | PhotoCapturedEvent | CaptureFailedEvent;

export interface QueuedPhoto {
  photoId: string;
  code: string;
  imageUrl: string;
  timestamp: string;
}

export interface GamepadButtonsConfig {
  next: number[];
  previous: number[];
  skipForward: number[];
  skipBackward: number[];
  triggerCapture: number[];
  triggerCapture1s: number[];
  triggerCapture3s: number[];
  triggerCapture5s: number[];
  toggleMode: number[];
}

export interface GamepadDpadAxesConfig {
  horizontalAxisIndex: number;
  verticalAxisIndex: number;
  threshold: number;
}

export interface GamepadConfig {
  enabled: boolean;
  debugMode: boolean;
  buttons: GamepadButtonsConfig;
  dpadAxes: GamepadDpadAxesConfig;
}

export interface ClientConfigDto {
  qrCodeBaseUrl: string | null;
  swirlEffect: boolean;
  slideshowIntervalMs: number;
  gamepad: GamepadConfig;
}

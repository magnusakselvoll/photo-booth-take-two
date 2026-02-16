# Photo Booth

A photo booth application for events. Runs unattended with slideshow display, photo capture, and web-based photo download.

## Features

- **Slideshow display**: Shows photos with Ken Burns effect (pan/zoom animations) and crossfade transitions
- **Photo capture**: Countdown timer triggered by click, touch, or keyboard, supports multiple rapid captures with queuing
- **Keyboard navigation**: Arrow keys to browse photos, R to toggle random/sorted, 1/3/5 for custom countdown durations
- **Photo download**: Guests retrieve photos via numeric code or QR code
- **Multiple camera support**: Webcam (via OpenCV), Android phone (via ADB over USB)
- **Internationalization**: English and Spanish language support with automatic detection and URL override (`?lang=es`)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (for web UI development)
- Windows (primary target), macOS/Linux (partial support)

## Quick Start

```bash
# Build
dotnet build

# Run tests
dotnet test

# Development (runs both backend and frontend with hot reload)
./run-dev.sh
```

Open `http://localhost:5173` in a browser to access the photo booth interface.

For production, build the frontend and run only the backend:
```bash
cd src/PhotoBooth.Web && pnpm run build
dotnet run --project src/PhotoBooth.Server
```

## Configuration

Configuration is done via `appsettings.json` in the Server project:

```json
{
  "Camera": {
    "Provider": "OpenCv",
    "OpenCv": {
      "CaptureLatencyMs": 100,
      "DeviceIndex": 0,
      "FramesToSkip": 5,
      "FlipVertical": false,
      "JpegQuality": 90,
      "PreferredWidth": 1920,
      "PreferredHeight": 1080,
      "InitializationWarmupMs": 500,
      "CaptureLockTimeoutSeconds": 5
    }
  },
  "Capture": {
    "CountdownDurationMs": 7000,
    "BufferTimeoutHighLatencyMs": 45000,
    "BufferTimeoutLowLatencyMs": 12000
  },
  "Input": {
    "EnableKeyboard": false
  },
  "PhotoStorage": {
    "Path": ""
  },
  "Trigger": {
    "RestrictToLocalhost": true
  },
  "NetworkSecurity": {
    "BlockOutboundRequests": true
  },
  "QrCode": {
    "BaseUrl": ""
  },
  "Event": {
    "Name": ""
  },
  "Slideshow": {
    "SwirlEffect": true,
    "IntervalMs": 30000
  },
  "RateLimiting": {
    "PermitLimit": 5,
    "WindowSeconds": 10
  }
}
```

### Camera Options

- `Provider`: Camera provider to use (`"OpenCv"`, `"Android"`, or `"Mock"`)

#### OpenCV Options (`Camera:OpenCv`)

- `DeviceIndex`: Webcam device index (0 = first camera)
- `CaptureLatencyMs`: Delay before capture to sync with countdown
- `FramesToSkip`: Number of frames to skip for auto-exposure adjustment
- `FlipVertical`: Mirror the image vertically
- `JpegQuality`: JPEG encoding quality, 1-100 (default: 90)
- `InitializationWarmupMs`: Camera warmup time on startup
- `PreferredWidth`/`PreferredHeight`: Requested camera resolution
- `CaptureLockTimeoutSeconds`: Seconds to wait for capture lock before reporting camera busy (default: 5)

### Android Camera Options (`Camera:Android`)

Requires [ADB](https://developer.android.com/tools/adb) installed and an Android phone connected via USB with USB debugging enabled.

- `AdbPath`: Path to ADB executable (default: `"adb"`)
- `DeviceImageFolder`: Device folder where camera saves photos (default: `"/sdcard/DCIM/Camera"`)
- `PinCode`: Optional PIN to unlock device screen
- `CameraAction`: Camera intent action (default: `"STILL_IMAGE_CAMERA"`)
- `FocusKeepaliveIntervalSeconds`: Periodic focus interval in seconds (default: 15)
- `FocusKeepaliveMaxDurationSeconds`: Max duration for focus keepalive in seconds (default: 180)
- `DeleteAfterDownload`: Delete photos from device after download (default: true)
- `FileSelectionRegex`: Regex to match photo files (default: `^.*\.jpg$`)
- `CaptureTimeoutMs`: Max wait for new photo in ms (default: 15000)
- `FileStabilityDelayMs`: Delay between file stability checks in ms (default: 200)
- `CapturePollingIntervalMs`: Polling interval for new files in ms (default: 500)
- `AdbCommandTimeoutMs`: Per-command timeout in ms (default: 10000)
- `CameraOpenTimeoutSeconds`: Seconds after which the camera is considered stale (default: 30)
- `MaxCaptureRetries`: Maximum number of capture retries after failure (default: 1)
- `CaptureLockTimeoutSeconds`: Seconds to wait for capture lock before reporting camera busy (default: 5)

### Other Options

- `Capture.CountdownDurationMs`: Countdown duration in ms before photo is taken (default: 7000)
- `Capture.BufferTimeoutHighLatencyMs`: Hard timeout buffer in ms for high-latency cameras (default: 45000)
- `Capture.BufferTimeoutLowLatencyMs`: Hard timeout buffer in ms for low-latency cameras (default: 12000)
- `Input.EnableKeyboard`: Enable spacebar key to trigger capture (default: false)
- `Trigger.RestrictToLocalhost`: Only allow trigger API from localhost (default: true)
- `NetworkSecurity.BlockOutboundRequests`: Block outbound network requests (default: true)
- `QrCode.BaseUrl`: Base URL for QR codes (defaults to request origin)
- `Event.Name`: Event name used as storage subfolder (defaults to current date)
- `Slideshow.SwirlEffect`: Enable swirl animation effect on slideshow (default: true)
- `Slideshow.IntervalMs`: Interval in ms between slideshow transitions (default: 30000)
- `RateLimiting.PermitLimit`: Max requests per rate limit window (default: 5)
- `RateLimiting.WindowSeconds`: Rate limit window duration in seconds (default: 10)

## Project Structure

```
src/
  PhotoBooth.Domain/        # Core entities and interfaces
  PhotoBooth.Application/   # Business logic and services
  PhotoBooth.Infrastructure/# Hardware and storage implementations
  PhotoBooth.Server/        # ASP.NET Core REST API + web UI
tests/
  PhotoBooth.Application.Tests/
  PhotoBooth.Infrastructure.Tests/
  PhotoBooth.Server.Tests/
```

## Acknowledgments

The Android camera integration is based on [android-photo-booth-camera](https://github.com/magnusakselvoll/android-photo-booth-camera).

## License

MIT
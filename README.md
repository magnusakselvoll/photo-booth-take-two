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

Configuration is done via `appsettings.json` in the Server project. You can delete `appsettings.json` to revert all settings to their defaults.

```json
{
  "Camera": {
    "Provider": "OpenCv",
    "DeviceIndex": 0,
    "CaptureLatencyMs": 100,
    "FramesToSkip": 5,
    "FlipVertical": false,
    "JpegQuality": 90,
    "PreferredWidth": 1920,
    "PreferredHeight": 1080,
    "InitializationWarmupMs": 500
  },
  "Capture": {
    "CountdownDurationMs": 3000
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
    "SwirlEffect": true
  }
}
```

### Camera Options

- `Provider`: Camera provider to use (`"OpenCv"`, `"Android"`, or `"Mock"`)
- `DeviceIndex`: Webcam device index (0 = first camera) — OpenCV only
- `CaptureLatencyMs`: Delay before capture to sync with countdown
- `FramesToSkip`: Number of frames to skip for auto-exposure adjustment — OpenCV only
- `FlipVertical`: Mirror the image vertically — OpenCV only
- `JpegQuality`: JPEG encoding quality, 1-100 (default: 90) — OpenCV only
- `InitializationWarmupMs`: Camera warmup time on startup — OpenCV only
- `PreferredWidth`/`PreferredHeight`: Requested camera resolution — OpenCV only

### Android Camera Options

Requires [ADB](https://developer.android.com/tools/adb) installed and an Android phone connected via USB with USB debugging enabled.

- `AdbPath`: Path to ADB executable (default: `"adb"`)
- `DeviceImageFolder`: Device folder where camera saves photos (default: `"/sdcard/DCIM/Camera"`)
- `PinCode`: Optional PIN to unlock device screen
- `CameraAction`: Camera intent action (default: `"STILL_IMAGE_CAMERA"`)
- `FocusKeepaliveIntervalSeconds`: Periodic focus interval (default: 15)
- `DeleteAfterDownload`: Delete photos from device after download (default: true)
- `FileSelectionRegex`: Regex to match photo files (default: `^.*\.jpg$`)
- `CaptureTimeoutMs`: Max wait for new photo (default: 15000)
- `CapturePollingIntervalMs`: Polling interval for new files (default: 500)
- `AdbCommandTimeoutMs`: Per-command timeout (default: 10000)

### Other Options

- `Input.EnableKeyboard`: Enable spacebar key to trigger capture (default: false)
- `Trigger.RestrictToLocalhost`: Only allow trigger API from localhost (default: true)
- `NetworkSecurity.BlockOutboundRequests`: Block outbound network requests (default: true)
- `QrCode.BaseUrl`: Base URL for QR codes (defaults to request origin)
- `Event.Name`: Event name displayed in the UI
- `Slideshow.SwirlEffect`: Enable swirl animation effect on slideshow (default: true)

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
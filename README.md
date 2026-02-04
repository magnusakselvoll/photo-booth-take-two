# Photo Booth

A photo booth application for events. Runs unattended with slideshow display, photo capture, and web-based photo download.

## Features

- **Slideshow display**: Shows photos with Ken Burns effect (pan/zoom animations) and crossfade transitions
- **Photo capture**: Countdown timer triggered by click, touch, or keyboard, supports multiple rapid captures with queuing
- **Keyboard navigation**: Arrow keys to browse photos, R to toggle random/sorted, 1/3/5 for custom countdown durations
- **Photo download**: Guests retrieve photos via numeric code or QR code
- **Multiple camera support**: Webcam (via OpenCV), mobile phone (Android - planned)
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
  }
}
```

### Camera Options

- `Provider`: Camera provider to use (`"OpenCv"` or `"Mock"`)
- `DeviceIndex`: Webcam device index (0 = first camera)
- `CaptureLatencyMs`: Delay before capture to sync with countdown
- `FramesToSkip`: Number of frames to skip for auto-exposure adjustment
- `FlipVertical`: Mirror the image vertically
- `InitializationWarmupMs`: Camera warmup time on startup
- `PreferredWidth`/`PreferredHeight`: Requested camera resolution

### Other Options

- `Input.EnableKeyboard`: Enable spacebar key to trigger capture (default: false)
- `Trigger.RestrictToLocalhost`: Only allow trigger API from localhost (default: true)
- `NetworkSecurity.BlockOutboundRequests`: Block outbound network requests (default: true)
- `QrCode.BaseUrl`: Base URL for QR codes (defaults to request origin)
- `Event.Name`: Event name displayed in the UI

## Project Structure

```
src/
  PhotoBooth.Domain/        # Core entities and interfaces
  PhotoBooth.Application/   # Business logic and services
  PhotoBooth.Infrastructure/# Hardware and storage implementations
  PhotoBooth.Server/        # ASP.NET Core REST API + web UI
tests/
  PhotoBooth.Application.Tests/
  PhotoBooth.Server.Tests/
```

## License

MIT
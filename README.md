# Photo Booth

A photo booth application for events. Runs unattended with slideshow display, photo capture, and web-based photo download.

## Features

- **Slideshow display**: Shows photos from the current event in rotation
- **Photo capture**: Countdown timer triggered by button press (keyboard, mouse, or joystick)
- **Photo download**: Guests retrieve photos via numeric code or QR code
- **Multiple camera support**: Webcam, mobile phone (Android)

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

# Run the server
dotnet run --project src/PhotoBooth.Server
```

The server starts at `http://localhost:5000`. Open this URL in a browser to access the photo booth interface.

## Configuration

Configuration is done via `appsettings.json` in the Server project:

```json
{
  "PhotoBooth": {
    "StoragePath": "./photos",
    "Camera": "Webcam",
    "CountdownSeconds": 5
  }
}
```

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
# CLAUDE.md

Instructions for Claude Code when working on this repository.

## Build Commands

```bash
dotnet build                              # Build all projects
dotnet test                               # Run all tests
dotnet run --project src/PhotoBooth.Server # Run the server

# Frontend (in src/PhotoBooth.Web)
pnpm install                              # Install dependencies
pnpm run dev                              # Dev server with hot reload (port 5173)
pnpm run build                            # Build to wwwroot
```

## Architecture

Clean architecture with four layers:

- **Domain** (`PhotoBooth.Domain`): Entities, interfaces, exceptions. No dependencies.
- **Application** (`PhotoBooth.Application`): Services, DTOs, business logic. Depends on Domain.
- **Infrastructure** (`PhotoBooth.Infrastructure`): Hardware and storage implementations. Depends on Domain.
- **Server** (`PhotoBooth.Server`): ASP.NET Core minimal API, serves REST endpoints and static web files. Depends on Application and Infrastructure.

### Key Interfaces (in Domain)

- `ICameraProvider`: Capture photos from different camera types
- `IInputProvider`: Handle trigger button input (keyboard, mouse, joystick)
- `IPhotoRepository`: Store and retrieve photos
- `IPhotoCodeGenerator`: Generate download codes for photos

### Key Services (in Application)

- `PhotoCaptureService`: Orchestrates countdown and photo capture
- `SlideshowService`: Manages slideshow photo selection

## Tech Stack

- **Backend**: C# / .NET 10, ASP.NET Core minimal APIs
- **Frontend**: React + TypeScript + Vite (source in `src/PhotoBooth.Web/`, builds to `src/PhotoBooth.Server/wwwroot/`)
- **Package manager**: pnpm (for supply chain security)
- **Platform**: Windows primary, macOS for development

## Dependency Policy

Minimize external dependencies. Only add well-established, widely-used libraries when genuinely needed. Examples of acceptable dependencies:
- Polly (resilience)
- Serilog (logging)
- OpenCvSharp (camera capture - if needed)

Avoid: niche libraries, multiple libraries solving the same problem, dependencies for trivial functionality.

## Coding Conventions

- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Prefer records for DTOs
- Use `CancellationToken` for async operations
- Throw domain-specific exceptions inheriting from `PhotoBoothException`
- Tests use MSTest with descriptive method names

## Current Implementation Status

**Done:**
- Domain layer (entities, interfaces)
- Application layer (services with tests)
- Infrastructure layer (mock camera, in-memory and file storage, input providers)
- Server layer (REST endpoints for photos, slideshow, camera, events)
- Server-Sent Events for real-time updates
- Web UI: BoothPage (slideshow + capture), DownloadPage (code entry + download)
- Webcam capture with two provider options: FlashCap and OpenCV
- Configuration via appsettings.json

**In Progress:**
- Testing OpenCV provider as alternative to FlashCap (added to address stability issues)

**TODO:**
- Migrate features and bugs from this file to github issues
- Android phone camera integration
- QR code display on photos
- Only allow trigger endpoint from localhost (configurable)

## Camera Provider Configuration

The application supports multiple camera providers, configured via `Camera:Provider` in appsettings.json:

| Provider | Value | Description |
|----------|-------|-------------|
| FlashCap | `"FlashCap"` | Default. Uses FlashCap library with persistent streaming. |
| OpenCV | `"OpenCv"` | Alternative using OpenCvSharp4. Simpler implementation. |
| Mock | `"Mock"` | For testing without a camera. |

Example configuration:
```json
{
  "Camera": {
    "Provider": "OpenCv",
    "DeviceIndex": 0,
    "FramesToSkip": 5,
    "JpegQuality": 90,
    "PreferredWidth": 1920,
    "PreferredHeight": 1080
  }
}
```

### FlashCap Provider Notes

The FlashCap provider has specific implementation details:

- **Persistent streaming**: Camera device stays open continuously (opening/closing per capture causes macOS AVFoundation crashes)
- **Frame skipping**: Each capture skips first N frames to allow camera auto-exposure to adjust
- **BMP/DIB format parsing**: FlashCap on macOS returns incorrect BMP header dimensions (reports 1552x1552 for 1920x1080 camera). The code detects actual dimensions from pixel count.
- **Pixel order**: macOS camera outputs ARGB format; configure via `Camera:PixelOrder` setting

### Known Issues

**Intermittent corruption in server captures**: The integration tests (WebcamCaptureTests) pass reliably 10/10, but the server sometimes produces corrupted photos (horizontal line patterns or black images). The camera sometimes outputs 1920x1080 frames and sometimes 1920x1088. Investigation ongoing - may be timing-related difference between test (1s delay) and server capture patterns.

### Testing Camera Providers

```bash
# Run FlashCap webcam integration tests
dotnet test tests/PhotoBooth.Infrastructure.Tests --filter "FullyQualifiedName~Webcam"

# Run OpenCV integration tests
dotnet test tests/PhotoBooth.Infrastructure.Tests --filter "FullyQualifiedName~OpenCv"
```

## Reference

- **SPEC.md**: Functional specification - describes how the application should work. Consult this for requirements and intended behavior.
- Previous implementation for Android integration patterns: https://github.com/magnusakselvoll/android-photo-booth-camera

# CLAUDE.md

Instructions for Claude Code when working on this repository.

## Issue Tracking

Issues are tracked in GitHub. Use `gh issue list` to see open issues and `gh issue view <number>` for details.

## Git Workflow (GitHub Flow)

Always use GitHub Flow when working on issues:

1. **Create a feature branch** from `main` before starting work:
   - First fetch and checkout latest main: `git fetch origin && git checkout main && git pull`
   - Branch name format: `<issue-number>-<short-description>` (e.g., `6-enhance-look-and-feel`)
   - Example: `git checkout -b 6-enhance-look-and-feel`

2. **Commit** changes with descriptive messages

3. **Push** the branch and **create a PR**:
   - **Ask before creating the PR** - the user may have feedback based on the console output or code
   - PR title should be descriptive of the change
   - Reference the issue in the PR body with `Closes #<issue-number>` to auto-close on merge
   - Use `gh pr create` for convenience

4. **Merge** after review (squash merge preferred for clean history)

## Documentation Updates

When closing issues via PR, consider updating:
- **SPEC.md** - Functional requirements, use cases, expected behavior
- **README.md** - Setup instructions, configuration, user-facing changes
- **CLAUDE.md** - Technical implementation details, architecture, known issues, build commands

## Build Commands

```bash
dotnet build                              # Build all projects
dotnet test                               # Run all tests
dotnet run --project src/PhotoBooth.Server # Run the server

# Frontend (in src/PhotoBooth.Web)
pnpm install                              # Install dependencies
pnpm run build                            # Build to wwwroot (required before running server)
pnpm run dev                              # Dev server with hot reload (port 5173)
```

## Architecture

Clean architecture with four layers:

- **Domain** (`PhotoBooth.Domain`): Entities, interfaces, exceptions. No dependencies.
- **Application** (`PhotoBooth.Application`): Services, DTOs, business logic. Depends on Domain.
- **Infrastructure** (`PhotoBooth.Infrastructure`): Hardware and storage implementations. Depends on Domain.
- **Server** (`PhotoBooth.Server`): ASP.NET Core minimal API, serves REST endpoints and static web files. Depends on Application and Infrastructure.

### Key Interfaces (in Domain)

- `ICameraProvider`: Capture photos from different camera types
- `IInputProvider`: Handle trigger button input (keyboard)
- `IPhotoRepository`: Store and retrieve photos
- `IPhotoCodeGenerator`: Generate download codes for photos

### Events (in Application)

- `IEventBroadcaster`: Broadcasts real-time events (countdown, capture, errors) to SSE clients

### Key Services (in Application)

- `PhotoCaptureService`: Orchestrates photo capture and storage
- `CaptureWorkflowService`: Manages countdown + capture + event broadcasting workflow
- `SlideshowService`: Manages slideshow photo selection

### Code Generation

- `SequentialCodeGenerator` (default): Assigns sequential numeric codes (1, 2, 3, ...) based on photo count

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

## Camera Provider Configuration

The application supports camera providers configured via `Camera:Provider` in appsettings.json:

| Provider | Value | Description |
|----------|-------|-------------|
| OpenCV | `"OpenCv"` | Default. Uses OpenCvSharp4 for cross-platform capture. |
| Android | `"Android"` | Uses Android phone via ADB over USB. Based on [android-photo-booth-camera](https://github.com/magnusakselvoll/android-photo-booth-camera). |
| Mock | `"Mock"` | For testing without a camera. |

Example OpenCV configuration:
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
  }
}
```

Example Android configuration:
```json
{
  "Camera": {
    "Provider": "Android",
    "AdbPath": "adb",
    "DeviceImageFolder": "/sdcard/DCIM/Camera",
    "PinCode": null,
    "CameraAction": "STILL_IMAGE_CAMERA",
    "FocusKeepaliveIntervalSeconds": 15,
    "FocusKeepaliveMaxDurationSeconds": 180,
    "DeleteAfterDownload": true,
    "FileSelectionRegex": "^.*\\.jpg$",
    "CaptureLatencyMs": 3000,
    "CaptureTimeoutMs": 15000,
    "FileStabilityDelayMs": 200,
    "CapturePollingIntervalMs": 500,
    "AdbCommandTimeoutMs": 10000
  }
}
```

## Additional Configuration

| Key | Description | Default |
|-----|-------------|---------|
| `Slideshow:SwirlEffect` | Enable swirl animation effect on slideshow | `true` |
| `Event:Name` | Event name (used as storage subfolder) | Current date |
| `QrCode:BaseUrl` | Base URL for QR code links | Request origin |
| `Capture:CountdownDurationMs` | Default countdown duration in ms | `3000` |

## Security

### HTTP Security Headers

All responses (including static files) include security headers added by `SecurityHeadersMiddleware`:

- **X-Content-Type-Options**: `nosniff` — prevents MIME-type sniffing
- **X-Frame-Options**: `DENY` — prevents clickjacking
- **Referrer-Policy**: `strict-origin-when-cross-origin` — limits referrer leakage
- **Permissions-Policy**: Disables camera, microphone, geolocation, payment browser APIs
- **Content-Security-Policy**: Restricts resource loading to `'self'` with exceptions for Google Fonts and inline styles (required by React)

No HSTS header — inappropriate for a local-network app with dynamic IPs.

### Rate Limiting

The `/api/photos/capture` and `/api/photos/trigger` endpoints are rate-limited using ASP.NET Core's built-in rate limiter: 5 requests per 10-second fixed window. Returns HTTP 429 when exceeded.

## CI/CD

GitHub Actions workflows in `.github/workflows/`:

- **ci.yml**: Runs on PRs and pushes to main. Builds .NET and frontend, runs tests, and lints.
- **release.yml**: Triggered when a release is published via the GitHub UI. Builds and uploads the Windows x64 executable to the release.

### Creating a Release

1. Create a release in the GitHub UI (with tag like `v1.0.0`)
2. The release workflow automatically triggers on publish
3. The Windows executable is built and uploaded to the release as an asset

## Reference

- **SPEC.md**: Functional specification - describes how the application should work. Consult this for requirements and intended behavior.
- Previous implementation for Android integration patterns: https://github.com/magnusakselvoll/android-photo-booth-camera

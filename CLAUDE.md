# CLAUDE.md

Instructions for Claude Code when working on this repository.

## Issue Tracking

Issues are tracked in GitHub. Use `gh issue list` to see open issues and `gh issue view <number>` for details.

## Git Workflow (GitHub Flow)

Always use GitHub Flow when working on issues:

1. **Create a feature branch** before making any file edits — no exceptions:
   - First fetch and checkout latest main: `git fetch origin && git checkout main && git pull`
   - Branch name format: `<issue-number>-<short-description>` (e.g., `6-enhance-look-and-feel`)
   - Create and checkout the branch: `git checkout -b 6-enhance-look-and-feel`
   - **Do not read or edit any files until the branch is created.** This prevents accidentally committing to main (direct pushes to main are blocked).
   - **Only use worktrees** when explicitly asked (e.g., "use a worktree", "work on several issues in parallel")

2. **Commit** changes with descriptive messages:
   - Write commit messages as plain double-quoted strings — no heredocs, no `$()` substitution
   - Each `-m` value must be a single line — newlines inside a `-m` string cause a "quoted characters in flag names" error
   - For multi-line messages use separate `-m` flags, one per line: `git commit -m "title" -m "body line"`

3. **Push** the branch and **create a PR**:
   - **Ask before creating the PR** - the user may have feedback based on the console output or code
   - PR title should be descriptive of the change
   - Reference the issue in the PR body with `Closes #<issue-number>` to auto-close on merge
   - Pass `--title` and `--body` as plain strings to `gh pr create` — no heredocs, no command substitution, and no backticks (backticks in strings trigger a command substitution approval prompt even when used as markdown formatting)
   - Always pass `--head <branch-name> --base main` to `gh pr create` — without these, `gh` picks up the main repo context and fails with "head branch is the same as base branch"

4. **Merge** after review (squash merge preferred for clean history)

5. **Clean up** after the user confirms a PR is merged:
   - `git fetch origin && git checkout main && git pull`
   - `git branch -d <branch-name>`

### Worktree usage (only when explicitly requested)

When the user asks to use a worktree or work on multiple issues in parallel:
   - Create a worktree: `git worktree add .claude/worktrees/6-enhance-look-and-feel -b 6-enhance-look-and-feel`
   - All file reads/edits/writes must use the full worktree path, e.g. `.claude/worktrees/<branch-name>/src/...`
   - Run all git commands in the worktree using `-C`: `git -C .claude/worktrees/<branch-name> <command>`
   - Do NOT use `cd .claude/worktrees/<branch-name> && git ...` — compound `cd` + `git` commands require special approval
   - Cleanup: `git -C <repo-root> worktree remove .claude/worktrees/<branch-name>` then `git -C <repo-root> branch -d <branch-name>`

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

## Test Classification

CI runs `dotnet test --filter "TestCategory!=Integration"`, so every new test class **must** be correctly classified:

- **Unit tests** (no attribute): Pure in-process tests using fakes/mocks. These run in CI.
- **Integration tests** (`[TestCategory("Integration")]`): Tests that require external hardware (camera, Android device via ADB), a real OS-level resource unavailable in CI (e.g. `Console.KeyAvailable` loop with real input), or long-running real-world scenarios. These are skipped in CI.

When writing a new test class, explicitly decide which category it belongs to and apply `[TestCategory("Integration")]` when appropriate. Do not add the attribute to tests that are actually unit tests just because they touch Infrastructure code.

## Camera Provider Configuration

The application supports camera providers configured via `Camera:Provider` in appsettings.json. Provider-specific settings are in subsections (`Camera:OpenCv`, `Camera:Android`), each with their own `CaptureLatencyMs`.

| Provider | Value | Description |
|----------|-------|-------------|
| OpenCV | `"OpenCv"` | Default. Uses OpenCvSharp4 for cross-platform capture. |
| Android | `"Android"` | Uses Android phone via ADB over USB. Based on [android-photo-booth-camera](https://github.com/magnusakselvoll/android-photo-booth-camera). |
| Mock | `"Mock"` | For testing without a camera. |

Example OpenCV configuration:
```jsonc
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
  }
}
```

Example Android configuration:
```jsonc
{
  "Camera": {
    "Provider": "Android",
    "Android": {
      "CaptureLatencyMs": 100,
      "AdbPath": "adb",
      "DeviceImageFolder": "/sdcard/DCIM/Camera",
      "PinCode": null,
      "CameraAction": "STILL_IMAGE_CAMERA",
      "FocusKeepaliveIntervalSeconds": 15,
      "FocusKeepaliveMaxDurationSeconds": 180,
      "DeleteAfterDownload": true,
      "FileSelectionRegex": "^.*\\.jpg$",
      "CaptureTimeoutMs": 15000,
      "FileStabilityDelayMs": 200,
      "CapturePollingIntervalMs": 500,
      "AdbCommandTimeoutMs": 10000,
      "CameraOpenTimeoutSeconds": 30,
      "MaxCaptureRetries": 1,
      "CaptureLockTimeoutSeconds": 5
    }
  }
}
```

## Additional Configuration

| Key | Description | Default |
|-----|-------------|---------|
| `Capture:CountdownDurationMs` | Countdown duration in ms before photo is taken | `7000` |
| `Capture:BufferTimeoutHighLatencyMs` | Hard timeout buffer for high-latency cameras | `45000` |
| `Capture:BufferTimeoutLowLatencyMs` | Hard timeout buffer for low-latency cameras | `12000` |
| `Slideshow:SwirlEffect` | Enable swirl animation effect on slideshow | `true` |
| `Slideshow:IntervalMs` | Interval in ms between slideshow transitions | `30000` |
| `Event:Name` | Event name (used as storage subfolder) | Current date |
| `QrCode:BaseUrl` | Base URL for QR code links | Request origin |
| `RateLimiting:PermitLimit` | Max requests per rate limit window | `5` |
| `RateLimiting:WindowSeconds` | Rate limit window duration in seconds | `10` |
| `Thumbnails:JpegQuality` | JPEG quality (0-100) for server-side resized thumbnails | `80` |
| `Booth:RestrictToLocalhost` | Redirect non-localhost users from `/` to `/download` | `true` |
| `Trigger:RestrictToLocalhost` | Restrict `/api/photos/trigger` to localhost only | `true` |
| `Capture:RestrictToLocalhost` | Restrict `/api/photos/capture` to localhost only | `true` |
| `Input:EnableKeyboard` | Enable spacebar to trigger capture | `false` |
| `NetworkSecurity:BlockOutboundRequests` | Block outbound HTTP requests | `true` |
| `PhotoStorage:Path` | Where photos are saved | OS-specific app data |
| `Watchdog:ServerInactivityMinutes` | Restart server after inactivity (0 to disable) | `30` |
| `Watchdog:ClientTimeoutMs` | Client-side watchdog timeout in ms | `300000` |
| `Watchdog:SseHeartbeatIntervalSeconds` | SSE heartbeat interval in seconds | `30` |

## User Configuration

`appsettings.json` is overwritten by the MSI installer on every upgrade. To persist customizations across upgrades, users create `appsettings.User.json` in the same directory. It is loaded after `appsettings.json` and overrides any values it contains — only include the settings you want to change.

Loading is handled by `UserSettingsLoader.AddUserSettings` (called in `Program.cs` after `CreateBuilder`). The file is optional; its absence is not an error. A startup log message is emitted when the file is present.

`appsettings.User.json` is excluded from the installer (not part of publish output) and from source control (`.gitignore`).

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

The `/api/photos/capture` and `/api/photos/trigger` endpoints are rate-limited using ASP.NET Core's built-in rate limiter. Defaults: 5 requests per 10-second fixed window (configurable via `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds`). Returns HTTP 429 when exceeded.

## CI/CD

GitHub Actions workflows in `.github/workflows/`:

- **ci.yml**: Runs on PRs and pushes to main. Builds .NET and frontend, runs tests, and lints.
- **release.yml**: Triggered when a release is published via the GitHub UI. Builds and uploads the Windows x64 standalone zip and MSI installer to the release.

### Creating a Release

1. Create a release in the GitHub UI (with tag like `v1.0.0`)
2. The release workflow automatically triggers on publish
3. Two artifacts are built and uploaded: a standalone zip and an MSI installer (both for Windows x64)

## Installer

The WiX-based MSI installer is in `installer/PhotoBooth.Installer/`.

- **Technology**: WiX Toolset v5.0.2
- **Install scope**: Per-user (`Scope="perUser"`) — no administrator privileges or UAC elevation required
- **Install path**: `%LOCALAPPDATA%\PhotoBooth`
- **Start Menu**: Creates a shortcut under the user's Start Menu
- **Upgrades**: Major upgrade; installing a newer version replaces the existing one (same-version reinstall also supported)
- **Suppressed ICEs**: ICE38, ICE40, ICE61, ICE64, ICE91 — required for the per-user install pattern

Build the MSI manually (after publishing the server):
```bash
dotnet build installer/PhotoBooth.Installer/PhotoBooth.Installer.wixproj --configuration Release -p:InstallerVersion=1.2.3 "-p:PublishDir=publish\win-x64\"
```

The version is extracted from the git tag in the release workflow (strips `v` prefix and any semver prerelease/build metadata for the MSI version field).

## Reference

- **SPEC.md**: Functional specification - describes how the application should work. Consult this for requirements and intended behavior.
- Previous implementation for Android integration patterns: https://github.com/magnusakselvoll/android-photo-booth-camera

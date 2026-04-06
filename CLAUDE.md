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
- MSTest analyzers enforce strict assertion methods (MSTEST0037 is an error, not a warning):
  - Use `Assert.HasCount(expected, collection)` not `Assert.AreEqual(expected, collection.Count)`
  - Use `Assert.IsEmpty(collection)` not `Assert.AreEqual(0, collection.Count)`
  - Use `Assert.Contains(substring, value)` not `Assert.IsTrue(value.Contains(substring))` — note: substring is first arg

## Test Classification

CI runs `dotnet test --filter "TestCategory!=Integration"`, so every new test class **must** be correctly classified:

- **Unit tests** (no attribute): Pure in-process tests using fakes/mocks. These run in CI.
- **Integration tests** (`[TestCategory("Integration")]`): Tests that require external hardware (camera, Android device via ADB), a real OS-level resource unavailable in CI (e.g. `Console.KeyAvailable` loop with real input), or long-running real-world scenarios. These are skipped in CI.

When writing a new test class, explicitly decide which category it belongs to and apply `[TestCategory("Integration")]` when appropriate. Do not add the attribute to tests that are actually unit tests just because they touch Infrastructure code.

## Configuration

See README.md for camera provider configuration, all appsettings.json keys and defaults, and user configuration (`appsettings.User.json`).

## Security

### HTTP Security Headers

All responses include security headers added by `SecurityHeadersMiddleware` (CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy). No HSTS — inappropriate for a local-network app with dynamic IPs.

### Rate Limiting

`/api/photos/capture` and `/api/photos/trigger` are rate-limited (default: 5 requests per 10-second fixed window, configurable via `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds`). Returns HTTP 429 when exceeded.

## CI/CD

GitHub Actions workflows in `.github/workflows/`:

- **ci.yml**: Runs on PRs and pushes to main. Builds .NET and frontend, runs tests, and lints.
- **release.yml**: Triggered when a release is published via the GitHub UI. Builds and uploads the Windows x64 standalone zip and MSI installer to the release.

### Creating a Release

1. Create a release in the GitHub UI (with tag like `v1.0.0`)
2. The release workflow automatically triggers on publish
3. Two artifacts are built and uploaded: a standalone zip and an MSI installer (both for Windows x64)

## Installer

WiX v5 MSI installer in `installer/PhotoBooth.Installer/`. Per-user install (`Scope="perUser"`, no admin/UAC) to `%LOCALAPPDATA%\PhotoBooth`. Major upgrade; installing a newer version replaces the existing one.

Build manually (after publishing the server):
```bash
dotnet build installer/PhotoBooth.Installer/PhotoBooth.Installer.wixproj --configuration Release -p:InstallerVersion=1.2.3 "-p:PublishDir=publish\win-x64\"
```

The version is extracted from the git tag in the release workflow (strips `v` prefix and semver prerelease/build metadata).

## Reference

- **SPEC.md**: Functional specification - describes how the application should work. Consult this for requirements and intended behavior.
- Previous implementation for Android integration patterns: https://github.com/magnusakselvoll/android-photo-booth-camera

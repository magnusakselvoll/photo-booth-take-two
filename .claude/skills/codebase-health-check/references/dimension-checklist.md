# Dimension Checklist — Codebase Health Check

Detailed review questions for each of the nine dimensions. Read this file at the start of Phase 1 to guide assessment. Each section maps to one subagent assignment.

---

## 1. Architecture & Code Organization

**Goal:** verify the clean-architecture layering is intact and the codebase stays coherent as it grows.

- [ ] **Dependency direction**: Domain has zero outward references. Application references only Domain. Infrastructure references Domain and Application. Server references all layers. Verify with `dotnet build` output or by scanning csproj `<ProjectReference>` entries.
- [ ] **Namespace / folder alignment**: each project's namespaces match its folder structure. No `.Domain` types used directly in `.Server` without going through `.Application`.
- [ ] **Dead code**: any unused classes, methods, or interfaces? Check with IDE warnings or search for types that are defined but never referenced.
- [ ] **Consistency**: naming conventions consistent (service suffix, repository suffix, provider suffix, etc.)? Any class doing too much (SRP violation)?
- [ ] **Solution file**: still using `.slnx` format (not `.sln`). Check `PhotoBooth.slnx` at repo root.
- [ ] **New layers or projects added** since the last health check? If so, do they fit the clean-arch model?
- [ ] **Front-end component structure**: are React components organised logically in `src/PhotoBooth.Web/src/`? Any obvious colocation issues (logic in UI components, no custom hooks, etc.)?

---

## 2. Documentation Accuracy & Drift

**Goal:** detect claims in docs that no longer match the code, config, or commands.

- [ ] **CLAUDE.md §Architecture**: verify the layer names, key interfaces (`ICameraProvider`, `IInputProvider`, `IPhotoRepository`, `IPhotoCodeGenerator`), key services (`PhotoCaptureService`, `CaptureWorkflowService`), and key events (`IEventBroadcaster`) still exist with those exact names.
- [ ] **CLAUDE.md §Build Commands**: run each listed command mentally — are the paths and flags still correct? E.g. `dotnet run --project src/PhotoBooth.Server`, `pnpm run build` in `src/PhotoBooth.Web`.
- [ ] **CLAUDE.md §Tech Stack**: still .NET 10, React + TypeScript + Vite, pnpm? Check `global.json` (if it now exists), `package.json`, and target framework in `Directory.Build.props`.
- [ ] **CLAUDE.md §Security**: does `SecurityHeadersMiddleware` still exist? Is the rate-limiting config still at `/api/photos/capture` and `/api/photos/trigger`? Still defaults 5 req / 10 s?
- [ ] **CLAUDE.md §Dependency Policy**: does Polly appear? (It should not — it is listed as an example of acceptable deps but was never added.) Any packages that violate the policy?
- [ ] **CLAUDE.md §Test Classification**: re-check the listed CI filter `--filter "TestCategory!=Integration"` against the actual CI workflow file.
- [ ] **README.md**: does it accurately describe current features (slideshow/Ken Burns, keyboard nav, download code/QR, webcam + Android-ADB cameras, EN/ES i18n)? Are installation steps still correct?
- [ ] **SPEC.md**: any functional behaviors described that the code contradicts?
- [ ] **`src/PhotoBooth.Web/README.md`**: still the unmodified Vite+React boilerplate? If so, it should either be removed or replaced with project-specific content.
- [ ] **Installer docs**: `CLAUDE.md §Installer` build command — is the path and flag syntax still valid?

---

## 3. Security

**Goal:** evaluate the actual implementation against the stated controls and catch any gaps.

- [ ] **SecurityHeadersMiddleware**: confirm CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy headers are set. Are the CSP directives appropriately restrictive for a local-network React app?
- [ ] **Rate limiting**: confirm middleware is registered and applied only to `/api/photos/capture` and `/api/photos/trigger`. Default config still 5 / 10 s?
- [ ] **Localhost gating**: `BoothLocalhostMiddleware` — confirm it blocks non-localhost requests on any endpoint that should be restricted.
- [ ] **Input validation**: are file paths used in `IPhotoRepository` sanitised? Any risk of path traversal?
- [ ] **Secrets**: are there any secrets, credentials, or API keys in config files, `appsettings.json`, or committed anywhere? (Check for `password`, `secret`, `key`, `token` patterns in `appsettings*.json` and `*.cs` files.)
- [ ] **Dependency CVEs**: check NuGet and npm packages against current advisories (see Dimension 4 — security findings should also be tagged `area:security`).
- [ ] **Download code brute-force**: `SequentialCodeGenerator` produces predictable numeric codes. Is there any rate-limiting or throttling on the download endpoint?
- [ ] **HTTP vs HTTPS**: the README/CLAUDE says no HSTS (appropriate for local network). Confirm no accidental HTTPS downgrade scenarios.
- [ ] **ADB / Android integration**: `AndroidCameraProvider` and `AdbService` — any security risk in the ADB command execution (command injection via device names or paths)?

---

## 4. Dependencies

**Goal:** confirm every dependency is necessary, licensed acceptably, actively maintained, and still the best choice. **Use WebSearch and WebFetch throughout this dimension.**

Start by reading the current package lists from the repo — do not rely on any hardcoded list:
- **Backend**: read `Directory.Packages.props` for all NuGet packages and versions.
- **Frontend**: read `src/PhotoBooth.Web/package.json` for all `dependencies` and `devDependencies`.

### For each backend (NuGet) package, ask:
- Is it still used? (Check for references in source — a package in `Directory.Packages.props` with no `<PackageReference>` in any csproj is a candidate for removal.)
- Is it a transitive pin? If so, has the direct dependency caught up so the pin is no longer needed?
- Is the pinned version current? Any newer version with bug fixes or security patches?
- Any known CVEs? (Search "[package name] NuGet CVE [current year]".)
- Is the license OSI-approved and compatible?
- Is the package actively maintained? (Check NuGet page for last publish date and download trend.)
- Is it still the right choice — or has a better-maintained or leaner alternative emerged?

### For each frontend (npm) package, ask:
- Is it still used in the source?
- Is the pinned version current? Any newer patch available?
- Any known security advisories? (Search "[package name] npm security [current year]".)
- Is the license acceptable?
- Is the package actively maintained? (Check npm page for last publish date; check GitHub for last commit and open issues.)
- For smaller niche packages: is it still the right choice, or could a lighter/more-maintained alternative serve better?

### License acceptability checklist
- MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause: ✅ acceptable.
- GPL, LGPL: ⚠️ check viral clause; may require disclosure.
- SSPL, BSL, Elastic License: ❌ not OSI-approved; flag.
- Unlicense / CC0: ✅ public domain.

---

## 5. Test Coverage & Usefulness

**Goal:** confirm tests are well-structured, cover important scenarios, and won't give false confidence.

- [ ] **Per-layer coverage**:
  - `PhotoBooth.Domain`: no test project (no logic to test — acceptable). Still true?
  - `PhotoBooth.Application.Tests`: covers `PhotoCaptureService`, `CaptureWorkflowService`, `UrlPrefixGenerator`. Any new services added without tests?
  - `PhotoBooth.Infrastructure.Tests`: covers file repo, HTTP handler, OpenCV, EventBroadcaster, code generator, input, camera (ADB/Android), imaging. Any new providers/services added without tests?
  - `PhotoBooth.Server.Tests`: covers endpoints, middleware, rate limiting, fallback route, networking, watchdog, settings. Any new endpoints without tests?
- [ ] **Frontend tests**: `src/PhotoBooth.Web/` — are Vitest tests present? Do they cover meaningful component behavior, not just rendering smoke tests?
- [ ] **`[TestCategory("Integration")]` correctness**: re-read CLAUDE.md's classification rule (real external hardware, OS-level resource unavailable in CI, long-running). Spot-check the 12 classified test classes. Any mis-classified unit tests (they'd be skipped in CI unnecessarily)?
- [ ] **Assertion quality**: CLAUDE.md §Coding Conventions lists MSTest analyzer rules (MSTEST0037). Scan for `Assert.AreEqual(expected, collection.Count)` instead of `Assert.HasCount`, `Assert.IsTrue(...Contains(...))` instead of `Assert.Contains`.
- [ ] **Test doubles**: are the stubs/fakes under `TestDoubles/` still aligned with their interfaces? (If an interface signature changed and the stub wasn't updated, tests may pass against a stale contract.)
- [ ] **Flaky tests**: any tests that depend on timing, file system state, or random data without deterministic seeding?
- [ ] **NBomber load test**: `GuestLoadTests` — is the baseline latency comment still accurate? Are the three scenarios (gallery_browse 60%, detail_view 30%, download 10%) still representative?

---

## 6. Observability & Logging

**Goal:** confirm the unattended booth is diagnosable when something goes wrong.

- [ ] **Serilog configuration**: is `appsettings.json` Serilog section configured (log level, sinks)? Appropriate defaults for production (not `Debug` globally)?
- [ ] **Log coverage**: are errors and warnings logged at the right level? Check exception handlers in `Program.cs` and service implementations.
- [ ] **Structured logging**: are log messages using structured templates (`Log.Information("Captured {PhotoId}", id)`) rather than string interpolation?
- [ ] **Sensitive data in logs**: any risk of logging PII, file paths that reveal user info, or download codes?
- [ ] **Startup logging**: does the application log its configuration (camera provider, listen addresses) on startup so an unattended booth operator can confirm correct boot?
- [ ] **Error surfacing to operator**: when a capture fails (camera unavailable, disk full, etc.), is the error surfaced clearly — in logs AND via the SSE event stream to the frontend?
- [ ] **Log rotation/retention**: any config for log file size/retention so the booth doesn't fill disk over months of operation?

---

## 7. CI/CD & Build Hygiene

**Goal:** confirm the build pipeline is healthy, strict, and reproducible.

- [ ] **`ci.yml`**: does it still correctly run `dotnet test --filter "TestCategory!=Integration"`? Are both `dotnet` and `frontend` jobs present? Does the frontend job run `pnpm run lint` and `pnpm run test`?
- [ ] **`release.yml`**: does the MSI/zip build still work? Does it correctly extract the version from the git tag?
- [ ] **`TreatWarningsAsErrors true`** in `Directory.Build.props`: still present? This is a quality gate — confirm no warnings are being suppressed with pragmas or `<NoWarn>`.
- [ ] **`global.json`** absent: pinning the .NET SDK version prevents "works on my machine" CI failures. Evaluate whether to add it (record the current SDK version from `dotnet --version`).
- [ ] **`.editorconfig`** absent: code style is enforced via compiler rules but an `.editorconfig` would make formatting consistent across editors. Evaluate whether to add it.
- [ ] **Central package management**: `Directory.Packages.props` — are all package versions defined there? Any package reference with an inline `Version="..."` in a csproj (which would bypass central management)?
- [ ] **`CentralPackageTransitivePinningEnabled`**: still enabled? Any transitive pins that may now be unnecessary (the pinned version has been incorporated into a direct dependency)?
- [ ] **`claude.yml`** workflow: still using a supported Claude Action version? Any deprecation warnings?
- [ ] **Installer subtree**: `installer/Directory.Build.props` is an empty `<Project />` to reset root props — is this still necessary and correct?

---

## 8. Performance & Resilience

**Goal:** confirm the booth handles hardware and network failures gracefully and doesn't degrade over long uptime.

- [ ] **Error handling in `PhotoCaptureService` and `CaptureWorkflowService`**: are all `async` calls wrapped with appropriate try/catch? What happens if the camera fails mid-capture?
- [ ] **OpenCV camera recovery**: if `OpenCvCameraProvider` loses the camera (USB unplug), does it recover on the next capture attempt or does it leave the system in a broken state?
- [ ] **Android/ADB recovery**: if the ADB connection drops, does `AndroidCameraProvider` reconnect or fail permanently until restart?
- [ ] **File system resilience**: if the photo storage directory fills up or becomes read-only, is this caught and surfaced gracefully?
- [ ] **SSE event stream**: if a browser client disconnects and reconnects, does the event stream resume correctly? Any resource leak from abandoned SSE connections?
- [ ] **`InactivityWatchdogService`**: what happens if the watchdog fires during an active capture? Is there a race condition?
- [ ] **Memory/resource leaks**: `OpenCvCameraProvider` uses `Mat` objects — are they disposed correctly? Any `IDisposable` implementations that may leak?
- [ ] **Long-running uptime**: any `static` state or in-memory collections that grow unboundedly (e.g. the photo gallery list — is it loaded from disk each request or cached in memory indefinitely)?
- [ ] **Polly** (absent): are HTTP calls (e.g. from `BlockingHttpHandler`) retried on transient failure? If not, evaluate whether a retry policy is needed.

---

## 9. Accessibility & i18n

**Goal:** confirm the guest-facing UI works for all guests.

### Accessibility
- [ ] **Image alt text**: do all `<img>` tags in the gallery and detail views have meaningful `alt` attributes (or `alt=""` for decorative images)?
- [ ] **Keyboard navigation**: can guests navigate the gallery and detail view without a mouse? Are interactive elements (buttons, links) focusable and have visible focus styles?
- [ ] **Colour contrast**: do text colours meet WCAG AA contrast ratios against their backgrounds? (Run a quick mental check on the design or note for manual testing.)
- [ ] **ARIA roles**: are modals, dialogs, or interactive overlays correctly annotated?
- [ ] **Zoom/pan (react-zoom-pan-pinch)**: is it accessible via keyboard or only via touch/mouse?
- [ ] **Reduced motion**: does the Ken Burns slideshow respect `prefers-reduced-motion`?

### Internationalisation
- [ ] **i18n coverage**: the app supports EN and ES. Are all user-visible strings in both translation files? Any hard-coded English strings in components?
- [ ] **Translation file locations**: where are translation files? Are they complete (no missing keys in either language)?
- [ ] **Date/number formatting**: are any dates or counts formatted with locale-specific methods?
- [ ] **RTL**: EN/ES are both LTR — no RTL concern currently. Note if a new language is added in future.

---

## Web Research Prompts (for Phase 1 dependency/security agents)

Before searching, read `Directory.Packages.props` for backend packages and `src/PhotoBooth.Web/package.json` for frontend packages to get the current list and pinned versions. Then for each package run a search of the form:

```
<package-name> CVE security vulnerability <current-year>
<package-name> npm maintained <current-year>         (for frontend packages)
<package-name> NuGet maintained <current-year>       (for backend packages)
```

For each package also visit its NuGet/npm page to check:
- Last publish date
- Weekly download trend (stability signal)
- Listed vulnerabilities
- Latest version vs pinned version in this repo

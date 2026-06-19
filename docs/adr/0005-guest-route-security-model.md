# 0005. Guest-route security model (salted URL prefix, no auth)

**Status:** Accepted

**Date:** 2024-01-01 (retroactive)

## Context

PhotoBooth runs on a local Wi-Fi network at events. Two distinct audiences access it:

- **Operator**: runs the booth, triggers captures, has physical access to the machine.
- **Guests**: browse and download their photos on their phones, connected to the same Wi-Fi.

Guests should not be able to trigger captures or access the booth operator page. Requiring guests to log in (username/password, OAuth, etc.) adds friction that would defeat the purpose of an event photo booth — guests expect to scan a QR code and immediately see their photos.

Additionally, if the event's photos are embarrassing or private, the app should not be trivially discoverable by anyone who finds the server's IP.

No public internet exposure is involved; HSTS is inappropriate because the IP address changes between events.

## Decision

Use a layered, no-login security model:

1. **Localhost restriction** (`LocalhostOnlyFilter`): The booth page (`/`), the capture API (`/api/photos/capture`), and the trigger API (`/api/photos/trigger`) only respond to requests from `127.0.0.1` / `::1`. External access returns HTTP 403. Configurable per endpoint via `Booth.RestrictToLocalhost`, `Capture.RestrictToLocalhost`, `Trigger.RestrictToLocalhost`.

2. **Salted URL prefix** for guest routes: Guest-facing routes (`/{prefix}/download`, `/{prefix}/gallery`, etc.) are prefixed with a short hash derived from `SHA256(eventName + salt)`. The salt is set by the operator in `appsettings.User.json` (`UrlPrefix:Salt`). This means the URLs are not guessable without knowing the salt, and changing the salt or event name invalidates old URLs (useful after an event ends).

3. **Security headers + rate limiting**: All responses include CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy headers (`SecurityHeadersMiddleware`). Capture and trigger endpoints are rate-limited (default: 5 requests per 10-second window).

4. **No HSTS**: The app runs over HTTP on a dynamic LAN IP; HSTS would lock browsers to an IP/hostname that changes between events.

## Consequences

- Guests get a frictionless experience: scan QR code, browse photos, download.
- The booth operator page and capture APIs are not accessible from guest devices.
- Guest URLs are obscure but not secret — anyone who sees the QR code can share the URL. This is intentional: guests sharing the URL with friends at the event is fine.
- Operators must distribute the QR code / URL to guests; the app itself does not do user management.
- An empty `UrlPrefix:Salt` still produces a deterministic (but weaker) prefix based on event name alone.

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| Login/password for guests | Kills the "scan and go" UX that makes photo booths work at events. |
| Per-photo access tokens | Significantly more complex; guests would need to carry tokens across sessions. |
| VPN / network segmentation | Requires IT infrastructure the operator typically does not have at a venue. |
| HTTPS + HSTS | Dynamic LAN IPs make certificate management impractical without a domain name; HSTS on IP addresses is not supported by browsers. |

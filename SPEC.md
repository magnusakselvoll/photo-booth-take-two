# Functional Specification

This document describes how the photo booth application should work. It serves as the authoritative source for functional requirements and design decisions.

## Overview

A photo booth application for non-commercial/event use. The booth runs unattended - no operator required. Guests interact directly with the device to take photos, which are displayed in a slideshow and can be downloaded via codes or QR links.

## Use Case

Typical deployment: A computer with monitor at a party or event. Guests walk up, press a button, pose during countdown, and see their photo. Later, they can download their photo using a code displayed on screen or scan a QR code.

## Display Modes

The application has two display modes:

### 1. Slideshow Mode (Idle)

When no photo is being taken, the screen shows a slideshow of photos from the current event.

- Photos rotate automatically at a configurable interval
- Selection is uniformly random from all event photos (can be toggled to sorted order)
- Each photo displays its download code (and optionally QR code)
- Fullscreen display optimized for the booth monitor
- **Ken Burns effect**: Photos display with subtle pan/zoom animations for visual interest
- **Crossfade transitions**: Smooth 0.5-second fade between photos
- **Keyboard navigation**: Arrow keys to navigate photos manually, R to toggle random/sorted order

### 2. Capture Mode (Active)

Triggered when a guest presses the capture button.

- **Countdown phase**: Display countdown timer (e.g., 5, 4, 3, 2, 1) so guests can prepare
- **Capture phase**: Take the photo
- **Preview phase**: Display the captured photo for a few seconds
- **Return to slideshow**: The new photo is added to the random rotation
- **Multiple captures**: Guests can trigger additional captures during countdown or preview; photos queue and display in order

## Photo Capture

### Trigger Input

Input methods:
- **Keyboard (backend)**: Configurable key (default: spacebar), must be explicitly enabled via configuration
- **Keyboard (web UI)**: Space/Enter to trigger capture, or 1/3/5 keys for custom countdown durations
- **Click/touch**: Click anywhere on the booth screen to trigger capture
- **HTTP API**: POST to `/api/photos/trigger` endpoint (restricted to localhost by default), accepts optional `durationMs` query parameter

### Camera Support

Multiple camera types supported through abstraction:

1. **Webcam**: USB webcam or built-in laptop camera using OpenCvSharp4 for cross-platform capture
2. **Mobile phone (Android)**: Phone connected via USB or network, using the phone's camera for higher quality photos (not yet implemented)

Camera selection is configurable. The system should handle camera disconnection gracefully and attempt reconnection.

#### Webcam Implementation Details

The OpenCV provider uses OpenCvSharp4 for cross-platform capture with configurable resolution, frame skipping for auto-exposure adjustment, and JPEG quality settings.

### Keyboard Shortcuts (Web UI)

The booth display supports keyboard shortcuts for navigation and capture:

| Key | Action |
|-----|--------|
| `←` / `→` | Previous / next photo |
| `↑` / `↓` | Skip 10 photos backward / forward |
| `R` | Toggle random / sorted order |
| `Space` / `Enter` | Trigger capture (default countdown) |
| `1` | Trigger capture with 1 second countdown |
| `3` | Trigger capture with 3 second countdown |
| `5` | Trigger capture with 5 second countdown |

Keyboard shortcuts are disabled during the countdown overlay to prevent accidental input.

### Countdown

- Configurable duration (default: 3 seconds)
- Visual countdown displayed prominently
- Audio feedback (beeps) — future enhancement
- Countdown cancellation — future enhancement

## Photo Storage and Retrieval

### Storage

- Photos stored locally on the server machine
- Organized by event/session
- Original quality preserved

### Download Codes

Each photo gets a unique, short, human-friendly code:
- Sequential numeric codes (1, 2, 3, ...) assigned in capture order
- Displayed on photo during slideshow
- Valid for the duration of the event

### QR Codes

Optional QR code overlay on slideshow photos:
- Links directly to photo download
- Guests scan with phone camera
- No app required - opens in browser

### Download Interface

Separate web page (or section) for photo retrieval:
- Guest enters their download code
- Photo displayed with download button
- **Photo grid view**: Shows all photos matching the code with download options
- Option to download original quality
- Works on mobile browsers

## Web Interface

The entire UI is web-based, served by the application server.

### Main Display (Booth Screen)

- Fullscreen-capable for kiosk mode
- Shows slideshow or capture countdown
- Minimal UI chrome - focus on photos
- Touch-friendly for touchscreen monitors

### Download Page

- Simple, mobile-friendly design
- Code entry field
- Photo preview and download

### Admin Interface (Future)

- View all photos from current event
- Delete photos
- Adjust settings
- Monitor system status

## Configuration

Key configurable parameters:

| Setting | Description | Default |
|---------|-------------|---------|
| `PhotoStorage:Path` | Where photos are saved | OS-specific app data |
| `Camera:Provider` | Camera provider to use | `OpenCv` |
| `Capture:CountdownDurationMs` | Countdown duration in ms | `3000` |
| `Slideshow:SwirlEffect` | Enable swirl effect on slideshow | `true` |
| `Event:Name` | Event name (used for storage folder) | Current date |
| `QrCode:BaseUrl` | Base URL for QR codes | Request origin |

## Internationalization (i18n)

The application supports multiple languages:
- **English** (default) and **Spanish** currently supported
- **Automatic detection**: Uses browser's `Accept-Language` header
- **URL override**: Append `?lang=es` or `?lang=en` to force a specific language
- **Footer selector**: Language can be changed via footer dropdown on download page

## Robustness Requirements

The application runs unattended, so reliability is critical:

1. **No lockups**: Errors must not freeze the UI. Graceful degradation preferred.
2. **Auto-recovery**: Attempt to recover from camera/input disconnections
3. **Logging**: Comprehensive logging for post-event debugging
4. **Watchdog**: Consider self-restart capability if application becomes unresponsive

## Non-Goals (Explicitly Out of Scope)

- Cloud storage or sync
- User accounts or authentication
- Payment processing
- Social media integration
- Video recording
- Multi-booth coordination
- Print functionality

## Future Considerations

Features that may be added later:

- Props/overlay graphics on photos
- Multiple photos in sequence (GIF or strip)
- Green screen / background replacement
- Remote monitoring via network
- Event presets / profiles

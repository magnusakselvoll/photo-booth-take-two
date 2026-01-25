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
- Selection is random, but recently taken photos are prioritized
- Each photo displays its download code (and optionally QR code)
- Fullscreen display optimized for the booth monitor

### 2. Capture Mode (Active)

Triggered when a guest presses the capture button.

- **Countdown phase**: Display countdown timer (e.g., 5, 4, 3, 2, 1) so guests can prepare
- **Capture phase**: Take the photo
- **Preview phase**: Display the captured photo for a few seconds
- **Return to slideshow**: The new photo is added to rotation with high priority

## Photo Capture

### Trigger Input

Multiple input methods supported:
- **Keyboard**: Configurable key (default: spacebar)
- **Mouse**: Click anywhere or specific button
- **Joystick/Gamepad**: External arcade-style button connected via USB

The input system must be robust - if one input method fails, others continue working.

### Camera Support

Multiple camera types supported through abstraction:

1. **Webcam**: USB webcam or built-in laptop camera
2. **Mobile phone (Android)**: Phone connected via USB or network, using the phone's camera for higher quality photos

Camera selection is configurable. The system should handle camera disconnection gracefully and attempt reconnection.

### Countdown

- Configurable duration (default: 5 seconds)
- Visual countdown displayed prominently
- Optional audio feedback (beeps)
- Cancellation possible (press button again or escape key)

## Photo Storage and Retrieval

### Storage

- Photos stored locally on the server machine
- Organized by event/session
- Original quality preserved
- Thumbnails generated for slideshow performance

### Download Codes

Each photo gets a unique, short, human-friendly code:
- Numeric only for easy verbal sharing (e.g., "4521")
- 4-6 digits depending on event size
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
| `StoragePath` | Where photos are saved | `./photos` |
| `Camera` | Camera type to use | `Webcam` |
| `CountdownSeconds` | Countdown duration | `5` |
| `SlideshowInterval` | Seconds between photos | `8` |
| `CodeLength` | Download code digits | `4` |
| `ShowQrCode` | Display QR codes on slideshow | `true` |

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

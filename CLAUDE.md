# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Photo booth application for non-commercial/event use. Runs unattended with slideshow display, photo capture via timer/button, and web-based photo download using numeric codes or QR codes.

## Tech Stack

- **Language:** C# / .NET 10
- **Architecture:** Server-client with REST API
- **Frontend:** Web-based UI
- **Platform:** Windows (initial target)

## Build Commands

```bash
dotnet build          # Build the solution
dotnet test           # Run tests
dotnet run            # Run the application
```

## Architecture

Server-client pattern:
- **Server:** Hardware interfaces (cameras, input buttons), photo storage, REST API
- **Web client:** Slideshow, countdown timer, user interface
- **Hardware abstraction:** Multiple camera types (webcam, mobile phone) and input devices (keyboard, mouse, joystick)

Design priorities: modular, testable, robust error handling for unattended operation.

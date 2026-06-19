# 0001. Clean architecture with four layers

**Status:** Accepted

**Date:** 2024-01-01 (retroactive)

## Context

PhotoBooth must support multiple camera backends (webcam via OpenCV, Android phone via ADB, mock for tests) and multiple storage backends, without the core business logic knowing which hardware is present. The application is also expected to be long-lived and actively tested, so testability matters.

## Decision

Structure the codebase as four distinct layers with a strict dependency rule (inner layers never depend on outer ones):

- **Domain** (`PhotoBooth.Domain`): entities, interfaces, domain exceptions. No external dependencies.
- **Application** (`PhotoBooth.Application`): services, DTOs, orchestration logic, event types. Depends only on Domain.
- **Infrastructure** (`PhotoBooth.Infrastructure`): concrete implementations of Domain interfaces — camera providers, photo storage, code generation, SSE event broadcasting. Depends on Domain.
- **Server** (`PhotoBooth.Server`): ASP.NET Core minimal API, middleware, endpoint mapping, static file serving. Depends on Application and Infrastructure.

Key interfaces live in Domain (`ICameraProvider`, `IInputProvider`, `IPhotoRepository`, `IPhotoCodeGenerator`), allowing Infrastructure implementations to be swapped without touching business logic.

## Consequences

- Camera and storage backends can be swapped or extended by adding an Infrastructure implementation; Application is unaffected.
- Unit tests can replace any Infrastructure component with a stub/fake without spinning up real hardware.
- The dependency inversion means wiring happens at composition root (Server/`Program.cs`), which is the one place that knows about all layers.
- Slightly more project files and ceremony compared to a single-project layout.

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| Single-project layout | All code in one project eliminates the structure enforced by project references, making accidental cross-layer coupling easy and invisible. |
| Feature-folder layout (vertical slices) | Natural for CRUD apps, but PhotoBooth has a single primary workflow (capture → store → display); horizontal layering is a better fit and makes hardware-abstraction boundaries explicit. |

# 0002. Server-Sent Events for real-time event broadcasting

**Status:** Accepted

**Date:** 2024-01-01 (retroactive)

## Context

The booth operator page and the frontend need to receive real-time push notifications from the server: countdown ticks, capture-complete signals, and error events. The flow is purely server → client; clients never push events back over this channel.

The project has a strict dependency-minimisation policy — external libraries are only added when genuinely needed. The target deployment is a local-network Windows machine with no external internet access.

## Decision

Use Server-Sent Events (SSE) over a plain HTTP endpoint for real-time push.

`IEventBroadcaster` (in Application) exposes `BroadcastAsync` and `SubscribeAsync`. The Infrastructure implementation (`EventBroadcaster`) fans out events to registered `Channel<PhotoBoothEvent>` subscribers. The Server layer exposes a `/api/events` SSE endpoint that subscribes and streams `text/event-stream` responses.

A periodic heartbeat (configurable via `Watchdog:SseHeartbeatIntervalSeconds`) keeps connections alive and lets the client-side watchdog detect stale connections.

## Consequences

- No external SignalR or WebSocket library required — SSE is built into `HttpResponse`.
- Browser reconnection is automatic via the `EventSource` API; no reconnection logic needed on the server.
- The abstraction (`IEventBroadcaster`) allows the Infrastructure implementation to be replaced (e.g., for testing with a stub) without changing Application or Server code.
- SSE is unidirectional (server → client only). If bidirectional communication ever becomes necessary, this decision would need to be revisited.
- Bounded channels (`Capacity = 100, FullMode = DropOldest`) protect memory if a slow client falls behind.

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| SignalR | Adds a significant library dependency; provides bidirectional RPC that is not needed here. SSE covers the use case with zero additional packages. |
| Raw WebSockets | More complex handshake and framing to implement manually; no auto-reconnect in browsers; still requires a library for production-quality use. |
| Polling | Simple but adds unnecessary latency to countdown display and wastes bandwidth on a battery-powered event laptop. |

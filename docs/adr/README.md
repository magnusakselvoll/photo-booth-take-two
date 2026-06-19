# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for the PhotoBooth project.

An ADR captures a significant architectural choice — the context that drove it, what was decided, and what alternatives were rejected. The goal is to make future maintenance less archaeological: instead of reverse-engineering *why* things are the way they are, read the relevant ADR.

## Status values

| Status | Meaning |
|--------|---------|
| Accepted | Decision is in effect |
| Superseded by [NNNN](NNNN-title.md) | Replaced by a later decision |
| Deprecated | Still in place but no longer recommended |

## When to write an ADR

Write one whenever you make a decision that is:

- **Consequential** — hard to reverse or costly to change later
- **Non-obvious** — a future reader might reasonably wonder "why not X?"
- **Cross-cutting** — affects multiple layers or components

Routine implementation choices (which utility method to use, how to name a variable) don't need ADRs.

## How to add one

1. Copy `0000-template.md` and number it sequentially.
2. Fill in all sections. "Alternatives considered" is the most important — it prevents future contributors from re-litigating already-resolved trade-offs.
3. Set status to `Accepted`.
4. Add a one-line entry to this README's index below.

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](0001-clean-architecture-layers.md) | Clean architecture with four layers | Accepted |
| [0002](0002-sse-for-realtime-events.md) | Server-Sent Events for real-time event broadcasting | Accepted |
| [0003](0003-pnpm-for-supply-chain-security.md) | pnpm as frontend package manager | Accepted |
| [0004](0004-per-user-msi-and-user-settings.md) | Per-user MSI install and appsettings.User.json | Accepted |
| [0005](0005-guest-route-security-model.md) | Guest-route security model (salted URL prefix, no auth) | Accepted |
| [0006](0006-sequential-code-generation.md) | Sequential numeric codes for photo download | Accepted |

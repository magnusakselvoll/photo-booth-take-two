# 0006. Sequential numeric codes for photo download

**Status:** Accepted

**Date:** 2024-01-01 (retroactive)

## Context

After a photo is taken, guests need a way to retrieve it on their phones. The booth displays a QR code and a short numeric code. Guests who cannot scan a QR code (older phone, poor lighting) type the numeric code into the download URL manually.

The code must be unique within an event, short enough to type without error, and generated without a separate coordination service.

## Decision

Use `SequentialCodeGenerator` as the default `IPhotoCodeGenerator`. It assigns codes `1`, `2`, `3`, … by incrementing the current photo count. The code for photo N is always `N` (count + 1 at capture time).

Because the count only ever increases, codes are inherently unique — the `isCodeTaken` callback on `IPhotoCodeGenerator.GenerateUniqueCodeAsync` is not needed and is intentionally ignored.

## Consequences

- Codes are as short as possible (1–3 digits for a typical event), minimising transcription errors.
- Guests can tell the operator "I'm photo number 42" unambiguously.
- Code generation is O(1) with no coordination or locking beyond the existing photo-count query.
- Codes are sequential and therefore enumerable: a guest could guess neighbouring codes and see other guests' photos. This is accepted — the download URL is already obscure (salted prefix, ADR 0005), and event photos are generally not considered sensitive. If strict isolation were needed, a different generator could be registered.

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| Random short codes (e.g., 6 alphanumeric chars) | Harder to type or read aloud; collision probability requires a retry loop; no meaningful benefit for typical event sizes (< 1 000 photos). |
| UUID / GUID | Far too long to type manually. |
| QR-only (no typed code) | QR scanning fails in poor lighting or on older devices; typed fallback is a real accessibility need. |

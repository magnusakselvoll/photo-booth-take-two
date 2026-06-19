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
- To raise the cost of *scripted* enumeration without abandoning short codes, the code-lookup endpoint (`GET /api/photos/{code}`) carries a per-IP fixed-window rate limit (the `"lookup"` policy; default 10 per 10s). This throttles a single client walking the code space while leaving legitimate guests, the gallery, the slideshow, and the bulk export script unaffected — the export script and gallery fetch the full photo list and download by GUID (`GET /api/photos/{id}/image`), never via the code lookup, so those paths are intentionally not throttled.
- Residual limitation: the list endpoint (`GET /api/photos/`) already returns every photo's GUID and code to any LAN client, and photos are shown in a public slideshow by design, so throttling the code lookup does not make photos private — consistent with the stance above that event photos are not treated as sensitive.

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| Random short codes (e.g., 6 alphanumeric chars) | Harder to type or read aloud; collision probability requires a retry loop; no meaningful benefit for typical event sizes (< 1 000 photos). |
| UUID / GUID | Far too long to type manually. |
| QR-only (no typed code) | QR scanning fails in poor lighting or on older devices; typed fallback is a real accessibility need. |

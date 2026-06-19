# 0003. pnpm as frontend package manager

**Status:** Accepted

**Date:** 2024-01-01 (retroactive)

## Context

The frontend (React + TypeScript + Vite) requires a JavaScript package manager. Supply-chain security is an explicit concern: malicious or compromised transitive dependencies are a real risk in the npm ecosystem. The project also targets Windows as primary deployment, so cross-platform behaviour of the package manager matters.

## Decision

Use **pnpm** as the package manager for `src/PhotoBooth.Web/`.

pnpm's strict `node_modules` layout (symlinked, content-addressed store) prevents packages from silently importing modules they did not declare as dependencies — a class of supply-chain vulnerability that npm's flat `node_modules` allows. The lockfile (`pnpm-lock.yaml`) pins exact content hashes for all transitive dependencies.

Additionally, pnpm's workspace and overrides mechanism (`pnpm.overrides` in `package.json`) makes it straightforward to apply security pins on transitive dependencies flagged by Dependabot without waiting for upstream patches.

## Consequences

- All contributors and CI must have pnpm installed (listed as a prerequisite in `README.md`).
- `pnpm-lock.yaml` is the authoritative lockfile; `package-lock.json` is not present.
- Transitive dependency security pins can be applied cleanly via `pnpm.overrides`.
- Phantom dependency access is prevented by design, which occasionally surfaces as import errors for packages that npm/yarn would have silently exposed.

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| npm | Flat `node_modules` allows phantom dependencies; no content-addressed store by default; weaker supply-chain guarantees. |
| yarn (classic) | Same flat layout issue as npm; classic yarn is in maintenance mode. |
| yarn berry (PnP) | Strong supply-chain story but Plug'n'Play mode has compatibility friction with some Vite/build-tool plugins; additional complexity for limited benefit over pnpm. |

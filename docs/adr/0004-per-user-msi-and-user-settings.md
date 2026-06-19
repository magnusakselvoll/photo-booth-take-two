# 0004. Per-user MSI install and appsettings.User.json for upgrade-safe configuration

**Status:** Accepted

**Date:** 2024-01-01 (retroactive)

## Context

PhotoBooth is installed on event laptops by non-technical operators who may not have administrator rights. Upgrades should be frictionless and must not overwrite operator-specific configuration (camera model, event name, storage path).

Standard ASP.NET Core practice is to ship a default `appsettings.json` that gets overwritten on upgrade, with no built-in mechanism for preserving local changes.

## Decision

**Installer scope:** The WiX v5 MSI installer (`installer/PhotoBooth.Installer/`) uses `Scope="perUser"`, installing to `%LOCALAPPDATA%\PhotoBooth`. No UAC elevation prompt is shown; no administrator rights are required.

**Configuration layering:** On startup, the server loads configuration from two files in the install directory:
1. `appsettings.json` — shipped defaults, overwritten on every upgrade.
2. `appsettings.User.json` — operator overrides, never touched by the installer.

Operators place only the settings they want to change in `appsettings.User.json`. The files are merged by ASP.NET Core's configuration system; keys in `appsettings.User.json` win.

## Consequences

- Upgrades are safe: operators run the new MSI, their customisations survive.
- No admin prompt on installation or upgrade; suitable for locked-down event machines.
- Operators must not edit `appsettings.json` directly — the `README.md` calls this out explicitly.
- If `appsettings.User.json` contains a key that is removed in a future version, the orphaned key is silently ignored by .NET's configuration system (no error, no warning).

## Alternatives considered

| Alternative | Why rejected |
|-------------|-------------|
| Per-machine MSI (ALLUSERS=1) | Requires admin rights / UAC — impractical for event laptops managed by non-IT staff. |
| Edit appsettings.json in place | Overwritten on upgrade, causing silent config loss. |
| Environment variables for overrides | Harder to discover and edit for non-technical operators; not persisted across reboots without additional OS setup. |
| Separate config UI | Adds significant UI development scope; a JSON file is sufficient given the operator profile. |

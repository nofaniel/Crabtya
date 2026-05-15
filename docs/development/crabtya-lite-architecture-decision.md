# Crabtya Lite Architecture Decision (Job 11)

Date: 2026-05-14
Status: Accepted for implementation kickoff

## Decision

Crabtya Lite will ship as a **separate plugin/project/package** from full Crabtya.

- New project path: `loader/Crabtya.Lite/`
- Separate release artifact: `packages/lite/Crabtya-Lite-v<version>.zip`
- Lite remains QoL-only and does not discover/load user mods.

## Why

- Avoids accidental cross-enable of full-loader systems in Lite.
- Keeps full Crabtya safety model intact for user-mod sessions.
- Gives users a clean, explicit product choice:
  - full Crabtya for folder mods/DLL/content surfaces;
  - Lite for curated built-in QoL settings only.

## Lite Product Contract (v1)

Lite includes only built-in QoL features:

- Mouse wheel zoom.
- Invert scroll setting.
- FOV setting in native-feeling settings UI.

Lite explicitly excludes:

- `game/Mods/` discovery and `eicmod.json` manifests.
- DLL mod loading and `Crabtya.ModApi` mod entrypoint lifecycle.
- Startup toggle queue (`startup-commands.json`) and mod state (`mod-state.json`).
- Dedicated Crabtya Mods window and mod enable/disable UX.
- Session-isolation save redirection and achievement blocking hooks.

## Technical Boundaries

Lite code must not reference or initialize these full-loader systems:

- `ModDiscovery`
- `ContentPipeline`
- `DllModLoader`
- `LiveModRegistry`
- `CrabtyaModsSettingsWindow`
- `SessionIsolationGuard`

Lite should reuse only narrowly-scoped applicator logic where safe (for example wheel/FOV applicators), either by extraction to shared internal helpers or by small duplicated Lite-owned implementations.

## Settings Persistence

Lite-owned settings persist under:

- `game/CrabtyaLite/settings.json`

This avoids overlap with full-loader mod state/settings files and keeps Lite data ownership explicit.

## Install/Compatibility Rules

- Full Crabtya and Crabtya Lite are not intended to be installed together unless explicitly tested and documented.
- Lite installer should detect full Crabtya plugin presence and warn/abort by default.
- Full Crabtya installer should detect Lite plugin presence and warn/abort by default.

## Implementation Sequence

1. Scaffold `loader/Crabtya.Lite/` project and minimal plugin bootstrap.
2. Add Lite settings storage (`CrabtyaLite/settings.json`) and typed options.
3. Implement zoom/invert/FOV applicators through Lite bootstrap only.
4. Add packaging/install scripts for Lite artifact and conflict checks.
5. Add user docs comparing Lite vs full Crabtya and explicit migration guidance.
6. Runtime-verify no mod discovery/loader state files and confirm settings persistence.

## Verification Targets

- Build Lite project cleanly with .NET 6.
- Launch with Lite only and confirm:
  - zoom/invert/FOV settings work and persist;
  - no `Mods/mod-state.json` or `Mods/startup-commands.json` behavior is introduced by Lite;
  - no `CrabtyaData/IsolatedSave` redirect behavior is introduced by Lite;
  - `ErrorLog.log` remains empty.

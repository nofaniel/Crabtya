# Safe Mode

## Purpose

Safe mode starts the game with all mods ignored for that launch. Use this when diagnosing crashes, broken mods, or version mismatch issues.

## Expected Behavior

- Mod discovery still reports installed mods but does not apply content/code.
- Main-menu Crabtya label may still appear; mod activation remains suppressed.
- Logs should clearly show safe mode activation and reason (if provided).

## Suggested Activation Paths

- Launch argument: `--eic-safe-mode`
- Alternate trigger: create `eic-safe-mode.flag` beside `Everything is Crab.exe`.

## Startup Toggle Commands

- Enable a mod on launch: `--eic-enable-mod=<mod-id>`
- Disable a mod on launch: `--eic-disable-mod=<mod-id>`
- Commands update persisted `Enabled` state in `Mods/mod-state.json`.
- Startup queue tooling writes the same next-launch intent format into `Mods/startup-commands.json`.
- Multiple commands are allowed in one launch; the last command for a given mod ID wins.
- In safe mode, startup toggles still update `Enabled`, but `EffectiveEnabled` remains `false` for that launch.
- If toggled mods declare `assemblies` and/or `content.catalogs`, loader marks `RestartRequired` and `RestartReasons` in `mod-state.json`.

## Current Runtime Contract

- Safe mode is evaluated on every launch.
- Command-line argument has priority over marker-file detection in logging reason.
- When safe mode is active:
  - discovery still runs and `Mods/mod-state.json` is updated,
  - each mod keeps user `Enabled` preference,
  - `EffectiveEnabled` is forced to `false` for that launch,
  - warning logs indicate safe mode suppression per mod.
- `mod-state.json` stores session fields:
  - `SafeModeActive` (`true`/`false`)
  - `SafeModeReason` (`none`, `command_line`, or `marker_file`)
  - `LastRunUtc` (ISO-8601 UTC timestamp)
- runtime queue/session fields currently relevant to restart behavior:
  - `StartupToggleApplied`
  - `StartupRequestedEnabled`
  - `StartupToggleSource`
  - `PendingStartupQueuedEnabled`
  - `PendingStartupQueuedSource`
  - `StartupToggles` summary (counts for CLI vs queued toggles, dedupe, unknown IDs, resolved intents)

## Quiet vs Verbose Localization Logging

- Current default is quiet localization lookup tracing.
- To opt in to verbose localization tracing for diagnosis:
  - launch with `--eic-localization-trace`, or
  - create `eic-localization-trace.flag` beside `Everything is Crab.exe`.

## Recovery Flow

1. Launch in safe mode.
2. Disable suspicious mods.
3. Relaunch normally.
4. Re-enable mods one by one to isolate failures.

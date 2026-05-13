# Job 05 - Harden Crabtya Mod API and Conflict Handling

Source TODO paragraph: "Ensure Crabtya is a full mod loader/API that mod makers can easily use to create new content/change content in the game. Crabtya should notify of conflicts and prevent loading of conflicting mods."

## Objective

Strengthen Crabtya as a practical v1 mod loader/API for mod makers by improving API ergonomics, documentation, templates, and conflict detection. Conflicting mods should produce clear user-facing and log-facing information, and unsafe conflicts should be prevented from loading together when Crabtya can prove the conflict.

## Scope

Work across `loader/Crabtya.ModApi/`, `loader/EIC.ModLoader/ModManifest.cs`, `ModDiscovery.cs`, content definition models/applicators, settings registry, Mods menu display, `templates/`, `docs/mod-makers/`, and `docs/users/troubleshooting.md`. This Job may be large; keep the first implementation pass focused on explicit conflict metadata and deterministic declarative conflicts, not impossible whole-game semantic conflict detection.

## Implementation Plan

1. Audit current public API and mod-maker docs for gaps: lifecycle, settings, logging, asset bundles, content definitions, path safety, dependency declarations, and examples.
2. Define a conflict model in manifests. Prefer explicit metadata first, such as `conflicts`, incompatible mod IDs/version ranges, and declarative resource claims.
3. Define deterministic content conflict rules for v1 surfaces where possible, such as multiple enabled mods writing the same localization key, same balance target, same camera patch target/setting ID, same visual target, same intro skip behavior, or same UI scale target.
4. Decide severity levels: informational overlap, warning conflict, and blocking conflict. Blocking should only occur when continuing would produce unsafe or unpredictable behavior.
5. Implement conflict detection during discovery/load-order resolution before content and DLL entrypoints run.
6. Persist conflict state into `mod-state.json` so the Mods menu can show why a mod is disabled or blocked.
7. Update the dedicated Mods settings window to surface conflict notices without reviving the old overlay panel.
8. Ensure broken or conflicting mods fail in isolation and do not block unrelated valid mods.
9. Expand mod-maker docs and templates with conflict/dependency examples.
10. Add runtime verification cases with at least two intentionally conflicting test mods.

## Deliverables

- Manifest/API support for explicit conflict declarations.
- Loader conflict detection with clear logs and state persistence.
- Mods menu conflict messaging.
- Docs and templates explaining how mod makers declare dependencies/conflicts and avoid common collisions.
- Test/sample conflict fixtures if useful, kept clearly separated from release sample mods.

## Verification

- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- Runtime: two conflicting mods produce clear `LogOutput.log` warnings/errors and only the safe subset loads.
- Runtime: an unrelated valid content-only mod and DLL mod still load when another mod is blocked for conflict.
- `mod-state.json` records conflict/blocked status clearly enough for UI display.
- `ErrorLog.log` remains empty during conflict handling.

## Completion Criteria

- Mod makers have enough API/docs/templates to build content and DLL mods without reverse-engineering the loader.
- Users get understandable conflict messages.
- Crabtya prevents conflicts it can prove, warns on overlaps it cannot safely adjudicate, and avoids false confidence for unknown semantic conflicts.

## Status

**Done (2026-05-13) — runtime verified**

All deliverables implemented, built (0 errors, 0 warnings), and runtime-confirmed. Two follow-up fixes also landed during validation:

- **Conflict notices now refresh immediately on hot-toggle** — the Mods window toggle callback calls `Refresh(savedScroll)` after a successful hot-toggle so `[!]`/`[*]` notices appear and clear without closing and reopening the window. Scroll position is preserved across the rebuild.
- **`[~]` → `[*]`** — the `~` glyph is absent from the game's TMP pixel-art font; replaced with `*` which renders correctly.

### What was implemented

- `loader/EIC.ModLoader/ModManifest.cs` — `conflicts: List<string>?` field added.
- `loader/EIC.ModLoader/ModStateStore.cs` — `ConflictStatus` (`"none"/"info"/"warning"/"blocked"`) and `ConflictNotices: List<string>` added to `ModStateEntry`; persisted to `mod-state.json`.
- `loader/EIC.ModLoader/ModConflictDetector.cs` — new file; two-pass detection:
  - Pass 1: explicit conflict declarations block the later mod in load order; symmetrical declarations deduplicated via `handledPairs`.
  - Pass 2: definition-ID overlap across all enabled mods (including blocked ones); warns both mods; localization skipped (by-design merge).
- `loader/EIC.ModLoader/ModDiscovery.cs` — `ModConflictDetector.Detect()` wired in between state-building and `ContentPipeline.Apply`; blocked mods have `EffectiveEnabled` set to `false` and status set to `conflictBlocked`; conflict notices and severity are written to `ModStateEntry`.
- `loader/EIC.ModLoader/CrabtyaModsSettingsWindow.cs` — `CreateConflictNoticeRows()` renders per-mod conflict notices in the Mods settings window; dark red `(0.55,0.20,0.16)` for blocked, amber-brown `(0.50,0.35,0.08)` for warning/info; prefix `[!]` for blocked, `[~]` for others.
- `loader/EIC.ModLoader/LiveModRegistry.cs` — `TryHotToggle` (content-only hot-toggle path) now re-runs conflict detection after each toggle without requiring a restart:
  - `ResetConflictState()` clears conflict fields on all in-memory entries and restores any previously conflict-blocked mods to their natural `Enabled && no-errors` state.
  - `ModConflictDetector.Detect()` runs against the updated enabled set.
  - Blocking and notice updates are applied to all affected in-memory entries.
  - `PersistAllModState()` saves all mod entries (not just the toggled one) so the Mods window's `Refresh()` reads updated conflict notices from disk without restart.
- `game/Mods/test.conflict-a/` — test fixture; `conflicts: ["test.conflict-b"]`; `balancePatch` id `test.shared-patch`; `defaultEnabled: false`.
- `game/Mods/test.conflict-b/` — test fixture; `balancePatch` id `test.shared-patch` (overlap target); `defaultEnabled: false`.
- `docs/mod-makers/manifest-format.md` — `conflicts` field documented.

### Runtime verification steps

1. Enable both `test.conflict-a` and `test.conflict-b` in the Mods menu; restart.
2. In `LogOutput.log`: confirm `Conflict: 'test.conflict-a' declares incompatibility with 'test.conflict-b'. Blocking 'test.conflict-b'` and `Conflict: balancePatch definition 'test.shared-patch' is claimed by both ...`.
3. In `mod-state.json`: confirm `test.conflict-b` has `conflictStatus: "blocked"` and `conflictNotices` populated; `test.conflict-a` has `conflictStatus: "info"` and `conflictNotices` with warning about the overlap.
4. In Mods settings window: confirm `[!]` blocked notice under `test.conflict-b` and `[~]` warning/info notices under `test.conflict-a`.
5. Confirm `ErrorLog.log` remains empty.
6. Enable only `test.conflict-a` (disable `test.conflict-b`); restart; confirm no conflict notices for either.

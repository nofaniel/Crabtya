# Agent Handoff

## Session Summary (2026-05-13) - Job 02 Git Publication

**Status**: Complete. Repository initialized, initial commit made, remote configured.

**What changed:**

- `.gitignore` created — excludes game binaries, BepInEx runtime, doorstop install files, runtime state (`mod-state.json`, `startup-commands.json`, `runtime-settings.json`), `CrabtyaData/`, build outputs (`**/bin/`, `**/obj/`), release package directories, draft brand assets, `opencode.json`, IDE files.
- `.gitattributes` created — `* text=auto eol=lf`; binary markers for `*.png`, `*.dll`, `*.zip`, `*.pdb`.
- Root `README.md` rewritten for GitHub: player install (PowerShell installer + manual), 6-mod included mods table, mod maker definitions table (all v1 surfaces), contributor section with build/package commands and key docs table, "what not to commit" guidance.
- `packages/README.md` updated with artifact descriptions, install instructions, fingerprint verification, exact regeneration command.
- `git init` completed; initial commit `60c04e4` — 142 files, 20916 insertions.
- Remote `origin` configured: `https://github.com/nofaniel/Crabtya.git`.
- `docs/development/jobs/job-02-git-initial-readiness.md` marked Done.
- `PLAN.md` Job 02 row updated to Done.

**What remains:**

- Push to remote is pending user instruction. The GitHub repository `https://github.com/nofaniel/Crabtya.git` may need to be created before pushing.
- When ready: `git push -u origin master`

---

## Session Summary (2026-05-13) - Job 10 Validation + Bug Fixes

**Status**: 10/15 checklist items confirmed from runtime logs. 4 bugs found and fixed during validation. Jobs 05, 06, 08 closed as fully done.

**Runtime evidence captured (from `LogOutput.log`):**

| # | Checklist item | Evidence |
|---|---|---|
| 1 | Plugin fingerprint | `Plugin binary fingerprint: Size=266752, LastWriteUtc=2026-05-13T16:27:37Z` |
| 2 | Content-only mod loads | `faniel.no-intro`, `faniel.mousewheel-zoom`, `test.conflict-a` in applied snapshot |
| 7 | Badge + MODS button | User-confirmed Launch 1 |
| 8 | MODS opens settings menu | User-confirmed Launch 1 |
| 9 | Pause MOD SETTINGS | User-confirmed Launch 1 |
| 10 | Settings persist | `runtime-settings.json` updated with invert key; user-confirmed |
| 12 | Content-only hot-apply | Conflict mods hot-toggled live without restart |
| 13 | ErrorLog empty | `ErrorLog.log` has 1 line (empty after camera fix) |
| 14 | Achievement blocking | `patched Steam_AchievementManager::UnlockAchievement`, `patched SteamUserStats::SetAchievement`, `patched SteamUserStats::IndicateAchievementProgress` |
| 15 | IsolatedSave redirect | `SessionIsolationGuard: base-game save path redirected into sandbox. Sample='.../CrabtyaData/IsolatedSave/Primary/Slot_1/Core.v1.dat'` |

**Not yet verified (require a run with DLL mods enabled):**
- Item 3: DLL-only mod loads (`DllModsLoaded=0` in current log — no DLL mod was enabled)
- Item 4: Hybrid mod loads
- Item 5: Broken manifest / missing DLL isolation
- Item 6: Throwing DLL entrypoint isolation
- Item 11: Startup-commands.json queue for restart-required mods

**Bugs found and fixed during validation:**

1. **`test.enemy-stat` manifest missing `targetGame` + `loaderVersion`** — mod silently skipped at startup; both fields added to `game/Mods/test.enemy-stat/eicmod.json`.

2. **Conflict notices required window close/reopen to refresh** — toggle callback now calls `Refresh(savedScroll)` after a successful hot-toggle; scroll position preserved. (`CrabtyaModsSettingsWindow.cs`)

3. **`[~]` glyph missing from game's TMP font** — replaced with `[*]` which renders correctly. (`CrabtyaModsSettingsWindow.cs`)

4. **`CameraPatchApplicator.GetLiveCameras()` NullReferenceException** — IL2CPP destroyed camera objects pass C# `is null` check but throw on property access. Both the prune loop and `RefreshCameraCache` now catch and discard invalid cameras. (`CameraPatchApplicator.cs`)

**Build**: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Next job: Job 02** — git/GitHub publication readiness.

---

## Session Summary (2026-05-13) - Job 10 Release Packaging Pass

**Status**: Release packaging complete. Full v1 validation checklist runtime-gated.

**What changed:**

- `tools/package-release.ps1` — three fixes/additions:
  1. Added `Get-ManifestString` helper and replaced all direct `$manifest.xxx` property accesses with it, fixing `Set-StrictMode -Version Latest` crashes on PSCustomObjects from `ConvertFrom-Json`.
  2. Added `-SkipTemplates` switch.
  3. Added `New-TemplatePackage` function + template output directory `packages/templates/` + template packaging loop; `package-summary.json` now includes `templatePackages` array.
- `packages/` rebuilt fresh at `v1.0.0`:
  - `packages/loader/EverythingIsCrab.ModLoader-v1.0.0.zip` — 266752-byte plugin (post-Jobs-03-09)
  - `packages/mods/faniel.{dll-sample,fov-slider,mousewheel-zoom,mvp-sample,no-intro,ui-scale}-1.0.0.zip` — all 0 warnings
  - `packages/templates/content-mod-template-v1.0.0.zip`
  - `packages/templates/dll-mod-template-v1.0.0.zip`
  - Test fixtures (`test.*`, `crabtya.bundle-test`, `persist.test`) excluded from release.
- `packages/README.md` — rewritten with artifact descriptions, install instructions, fingerprint verification, and exact regeneration command.
- `docs/development/jobs/job-10-v1-validation-release-packaging.md` — updated with packaging pass results and 15-item checklist status table.
- `PLAN.md` — Job 10 status updated to `In progress (2026-05-13, runtime validation pending)`.

**Build**: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**What remains (all runtime-gated):**

The full 15-item v1 Validation Checklist requires a live game run:
1. Enable test mods as needed and launch the game.
2. Check `game/BepInEx/LogOutput.log` for plugin fingerprint, mod load lines, settings/save behavior.
3. Check `game/BepInEx/ErrorLog.log` stays empty.
4. Verify main-menu Crabtya badge, `MODS` button, pause-menu `MOD SETTINGS`, settings persistence, hot-toggle vs queued-restart behavior.
5. Verify achievement blocking and isolated-save paths.

Also pending from earlier Jobs (all require running the game):
- **Job 05**: enable `test.conflict-a` + `test.conflict-b`, toggle in Mods menu, confirm `[!]`/`[~]` notices appear/clear without restart.
- **Job 06**: open Mods menu with `faniel.mousewheel-zoom` enabled, confirm `Invert Scroll` row; toggle it and confirm zoom direction changes and `runtime-settings.json` updated.
- **Job 08**: enable `test.enemy-stat`, enter a run, confirm `EnemyStatPatchApplicator: patch applied` lines in `LogOutput.log`.
- **Job 09**: supply Unity 6000.2.15f1 StandaloneWindows64 bundle at `game/Mods/crabtya.bundle-test/bundles/test-assets`, enable mod, restart, confirm bundle load lines.

Once runtime validation is captured for all 15 items, mark Job 10 complete and proceed to Job 02 (git/GitHub publication).

---

## Session Summary (2026-05-13) - Plan Notes for Logos and GitHub Remote

Docs-only planning update. No code/build/runtime changes.

**What changed:**

- `PLAN.md` now records that finalized logo files exist at repo root as `logo-L.png`, `logo-M.png`, and `logo-S.png`.
- `PLAN.md`, `docs/development/jobs/job-02-git-initial-readiness.md`, and `docs/development/jobs/job-04-loader-branding.md` now tell future branding/GitHub/publication work to prefer those root logo files over older draft assets.
- Job 02 now records the intended private GitHub remote: `https://github.com/nofaniel/Crabtya.git`.
- Job 02 now explicitly says to initialize/add the project to git and configure that remote once the `.gitignore`, README, and publication safety review confirm no local game/runtime/private artifacts would be committed.

**Verification:**

- Located logo files with `rg --files`: `logo-L.png`, `logo-M.png`, `logo-S.png`.
- `git status --short` currently reports this workspace is not a git repository, so Job 02 still needs to perform the safe init/publish flow.

---

## Session Summary (2026-05-13) - Job 08 Enemy Stat Patching Surface

**Status**: Code complete, built 0 errors/0 warnings. Runtime verification pending.

**What changed:**

- `loader/EIC.ModLoader/EnemyStatPatchApplicator.cs` (new, ~420 lines) — Harmony postfix on `World.Characters.Enemies.BasicEnemyCharacter.InitEnemyInstanceStatsIfNeeded`. Resolves `EnemyStats` from `EntityStats` property. Supports two target prefixes:
  - `enemy.stat.<statName>` — applies to every enemy regardless of archetype
  - `enemy.<EEnemyArchetype>.stat.<statName>` — applies only when `BasicEnemyCharacter.EnemyArchetype` matches
  - Operations: `set`, `add`, `multiply` (mirrors player stat vocabulary)
  - Patch application mirrors `BalancePatchApplicator.cs`: property/field reflection with `FlatValue`/`PercentageValue`/`PercentageMultiplier` value-holder support, numeric direct-set support, and failure-isolated per-patch logging

- `loader/EIC.ModLoader/Plugin.cs` — `EnemyStatPatchApplicator.Install(Log)` added after `BalancePatchApplicator.Install(Log)`

- `game/Mods/test.enemy-stat/` — test fixture (defaultEnabled: false), two balance patches:
  - `test.enemy-stat.global-maxhp`: `enemy.stat.MaxHp multiply 0.5` (all enemies)
  - `test.enemy-stat.blobfish-speed`: `enemy.BlobFish.stat.MovementSpeed multiply 2.0`

**Probe findings (static analysis via .NET 6 MetadataLoadContext inspector):**

| Item | Value |
|---|---|
| Hook type | `World.Characters.Enemies.BasicEnemyCharacter` |
| Hook method | `InitEnemyInstanceStatsIfNeeded` |
| Stats class | `World.Stats.EnemyStats` |
| Stats inheritance | `EnemyStats → AEnemyStats → AEntityStats → Il2CppSystem.Object` |
| Archetype | `EEnemyArchetype` enum (59 values confirmed) |
| Methods on EnemyStats | `Clone`, `DoGetStatValue` |
| PlayerStats methods NOT present | `ResetStatsToDefaultState`, `MarkStatsWhichCanBeReceiveStatChangeMultiplierEffect`, `EnableEnforceRangeLimits` — these exist only on `PlayerStats`, not on `EnemyStats` or its base classes |

**No changes required in:**
- `ContentPipeline.cs` — balance patches already route through `BalancePatches` dictionary
- `ContentDefinitions.cs` — `BalancePatchDefinition.Target` is a freeform string
- `ContentPipeline.Apply()` — accepts any `target` string for `balancePatch` type

**Docs updated:**
- `docs/development/architecture.md` — added `EnemyStatPatchApplicator` to component table + new Enemy Stat Patch Model section
- `docs/mod-makers/manifest-format.md` — removed `enemy` from "future-facing" list, added `balancePatch` examples for `enemy.stat.*` and `enemy.<archetype>.stat.*`, documented confirmed EEnemyArchetype values and EnemyStats properties
- `docs/development/jobs/job-08-enemy-stat-patching.md` — marked Done (2026-05-13), all findings and implementation notes recorded
- `PLAN.md` — Job 08 status updated to Done; recommended next task updated to Job 10

**Build**: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Runtime verification steps for Job 08:**
1. Enable `test.enemy-stat` in mod-state.json or via the Mods settings window (requires restart — it's a balance patch mod)
2. Start game, enter a run with enemies
3. Check `game/BepInEx/LogOutput.log` for `EnemyStatPatchApplicator: hook installed` and `EnemyStatPatchApplicator: patch 'test.enemy-stat.global-maxhp' applied`
4. Verify `ErrorLog.log` remains empty

---

## Session Summary (2026-05-13) - Job 05 Hot-Conflict Follow-up

User requested that conflict warnings update without a game restart when toggling content-only mods in the Mods settings window.

**What changed:**

- `loader/EIC.ModLoader/LiveModRegistry.cs` — `TryHotToggle` now runs a full conflict re-detection pass after each hot-toggle:
  - New `ResetConflictState()` clears `ConflictNotices`, `ConflictStatus`, and restores `EffectiveEnabled` for any previously conflict-blocked content mod.
  - `ModConflictDetector.Detect()` runs against the updated in-memory enabled set.
  - Block and notice results are applied to all affected entries.
  - `PersistModStateEntry` replaced by `PersistAllModState()` which writes all mod entries to `mod-state.json` in a single save; ensures the Mods window's per-`Refresh()` disk read picks up conflict changes for mods other than the one that was toggled.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Runtime verification steps:**

1. Enable both `test.conflict-a` and `test.conflict-b` in the Mods menu (open from main menu `MODS` button).
2. After each toggle the window calls `Refresh()` — without restarting, the conflict `[!]` notice should appear under `test.conflict-b` and `[~]` under `test.conflict-a`.
3. Disable `test.conflict-a` (while leaving `test.conflict-b` enabled); after the toggle, all conflict notices should disappear.
4. Re-enable `test.conflict-a`; conflict notices should reappear immediately.
5. Confirm `LogOutput.log` shows the conflict detection log lines on each toggle.
6. Confirm `ErrorLog.log` stays empty.

**Scope note:** DLL and bundle mods require restart when toggled (they never enter `TryHotToggle`). For those, conflict detection already re-runs at startup. The hot-conflict path only applies to content-only mods.



Implemented the full conflict detection and surfacing system for Crabtya v1. Built (0 errors, 0 warnings). Runtime verification is pending.

**What changed:**

- `loader/EIC.ModLoader/ModManifest.cs` — `conflicts: List<string>?` field added (JSON property `"conflicts"`), between `dependencies` and `content`.
- `loader/EIC.ModLoader/ModStateStore.cs` — `ConflictStatus` (`"none"/"info"/"warning"/"blocked"`) and `ConflictNotices: List<string>` added to `ModStateEntry`; serialized to `mod-state.json` so the UI can read them.
- `loader/EIC.ModLoader/ModConflictDetector.cs` — new file implementing two-pass conflict detection:
  - Pass 1: explicit conflict declarations — when two enabled mods share a `conflicts` link, the one later in load order is added to `BlockedModIds`; symmetrical declarations deduplicated via `handledPairs`.
  - Pass 2: definition-ID overlap — scans all enabled mods' definition files; tracks `(type:id)` → first-owner map; warns both mods on overlap; skips `localization` (by-design merge).
- `loader/EIC.ModLoader/ModDiscovery.cs` — `ModConflictDetector.Detect()` wired between state-building and `ContentPipeline.Apply`; blocked mods get `EffectiveEnabled = false` and `Status = "conflictBlocked"`; conflict notices and severity written to `ModStateEntry`.
- `loader/EIC.ModLoader/CrabtyaModsSettingsWindow.cs` — `CreateConflictNoticeRows()` renders conflict notices inside each mod block; dark red for blocked (`[!]`), amber-brown for warning/info (`[~]`).
- `game/Mods/test.conflict-a/` — test fixture; declares `conflicts: ["test.conflict-b"]`; has `balancePatch` id `test.shared-patch`; `defaultEnabled: false`.
- `game/Mods/test.conflict-b/` — test fixture; has `balancePatch` id `test.shared-patch` (overlap target); `defaultEnabled: false`.
- `docs/mod-makers/manifest-format.md` — `conflicts` field documented with example and one-directional declaration note.
- `docs/development/jobs/job-05-mod-api-conflicts.md` — marked Done (2026-05-13) with full implementation notes and runtime verification steps.
- `PLAN.md` — Job 05 status updated to `Done (2026-05-13, runtime verification pending)`; recommended next task updated to Job 08.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Runtime verification steps for Job 05:**

1. Enable both `test.conflict-a` and `test.conflict-b` in Mods menu; restart game.
2. In `LogOutput.log`: confirm `Conflict: 'test.conflict-a' declares incompatibility with 'test.conflict-b'. Blocking 'test.conflict-b'` log line and the definition-overlap warning for `balancePatch 'test.shared-patch'`.
3. In `mod-state.json`: confirm `test.conflict-b` has `conflictStatus: "blocked"` and non-empty `conflictNotices`; `test.conflict-a` has `conflictStatus: "info"` (or `"warning"` due to overlap) and its own notices.
4. In Mods settings window: confirm `[!]` notice under `test.conflict-b` and `[~]` notice under `test.conflict-a`.
5. `ErrorLog.log` stays empty.
6. Enable only `test.conflict-a` (disable `test.conflict-b`); restart; confirm no conflict notices for either mod.

**Recommended next Job:** Job 08 — enemy stat patching surface.



User confirmed runtime validation: tan/parchment badge with dark-brown "Crabtya v1.0.0" text appears top-right on main menu, hides correctly during gameplay, returns on menu. Docs-only closure pass.

**What changed:**

- `docs/development/jobs/job-04-loader-branding.md` — status updated to Done (2026-05-13) with runtime evidence note.
- `PLAN.md` — Job 04 status updated to `Done (2026-05-13)`; recommended next task set to Job 05.

**Build result:** not rerun (docs/status-only closure pass).



Implemented the polished native-feeling Crabtya badge in `loader/EIC.ModLoader/RuntimeOverlay.cs`. Runtime verification is still required.

**What changed:**

- `loader/EIC.ModLoader/RuntimeOverlay.cs`:
  - Added `_badgeContainer` (`GameObject?`) field.
  - Replaced the single red `Text` object with a three-object hierarchy:
    - `Crabtya.Badge.Container` — `RectTransform` 200×28px, top-right at `(-12, -12)`, tracked by `_badgeContainer`.
    - `Crabtya.Badge.Background` — `Image` filling the container, native tan/parchment `(0.88, 0.78, 0.60, 0.88)`, `raycastTarget = false`.
    - `Crabtya.Badge.Text` — `Text` inside the container (6px horizontal padding), dark brown `(0.24, 0.16, 0.10, 0.95)`, size 14, bold, centered.
  - Text shortened from `"Crabtya loaded v{version}"` to `"Crabtya v{version}"` for a cleaner badge appearance.
  - `UpdateBadgeVisibility()` now toggles `_badgeContainer.SetActive(show)` so background and text hide/show together; kept null-guard on `_badgeText` for the text assignment.
- `docs/development/jobs/job-04-loader-branding.md` — status updated to in-progress; implementation notes and remaining verification steps added.
- `PLAN.md` — Job 04 status updated to `In progress (2026-05-13, runtime verification pending)`; recommended next task updated.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Remaining for Job 04 completion (runtime verification required):**

1. Launch the game, reach the main menu.
2. Confirm top-right corner shows a tan/parchment panel with dark-brown `"Crabtya v1.0.0"` text.
3. Start a game run; confirm the badge hides during gameplay.
4. Return to main menu; confirm the badge reappears.
5. Open Mods menu; confirm it still works without interference.
6. Confirm `game/BepInEx/ErrorLog.log` remains empty.
7. Once verified, update `job-04-loader-branding.md` and `PLAN.md` to mark complete.



Started Job 09 from `PLAN.md`. The loader-side `AssetBundleApplicator` was already complete. This pass built all infrastructure needed for the end-to-end proof and documented the Unity build recipe. Runtime success-path verification is gated on the user supplying a Unity 6000.2.15f1 bundle.

**What changed:**

- `game/Mods/crabtya.bundle-test/` — new hybrid test mod:
  - `eicmod.json` — declares `content.assetBundles: ["bundles/test-assets"]`, `assemblies: ["bin/crabtya.bundle-test.dll"]`, `entrypoints: ["Crabtya.BundleTest.BundleTestEntrypoint"]`, `defaultEnabled: false`.
  - `src/BundleTestEntrypoint.cs` — calls `AssetBundleApplicator.GetBundle()`, lists all asset names in the bundle, and tries to load a `Texture2D` named `test_texture`. Logs clearly for success and failure cases.
  - `src/crabtya.bundle-test.csproj` — references `Crabtya.ModApi.dll`, `EIC.ModLoader.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.AssetBundleModule.dll`, `Il2CppInterop.Runtime.dll`, `Il2Cppmscorlib.dll`. Output to `bin/`.
  - `bin/crabtya.bundle-test.dll` — built 0 errors / 0 warnings.
  - `bundles/` directory created (user drops bundle file here).
- `docs/mod-makers/asset-workflow.md` — rewritten with full Unity 6000.2.15f1 AssetBundle build recipe: Unity project setup, asset bundle assignment in editor, Editor build script (`BuildPipeline.BuildAssetBundles` for StandaloneWindows64), output deployment, DLL project reference requirements, failure log table, and quick reference.
- `docs/mod-makers/manifest-format.md`:
  - `content.assetBundles` moved from "possible but not final-stable" to the active v1 surface list.
  - Validation rules entry updated with retrieval pattern (`AssetBundleApplicator.GetBundle`) and link to asset-workflow doc.
- `docs/mod-makers/quickstart.md` — new Section 4 "Bundle Mod" with folder layout, manifest snippet, and DLL entrypoint example; later sections renumbered.
- `docs/development/jobs/job-09-assetbundle-proof.md` — status updated to in-progress; all completed steps marked; remaining runtime verification steps listed with exact expected log lines.
- `PLAN.md`:
  - Job 09 status updated to `In progress (2026-05-13, runtime proof pending)`.
  - "Recently landed" AssetBundle note updated to reflect infrastructure-proven state.
  - Recommended next task updated to runtime verification instructions.

**Build result:** `dotnet build game/Mods/crabtya.bundle-test/src/crabtya.bundle-test.csproj -c Release` — 0 errors, 0 warnings.

**Remaining for Job 09 completion (user action required):**

1. **Build the test bundle** in Unity 6000.2.15f1:
   - Create a Unity project using Unity 6000.2.15f1.
   - Import a PNG texture, name the asset `test_texture`, assign it to an AssetBundle named `test-assets`.
   - Run `Assets > Build AssetBundles` (StandaloneWindows64) using the Editor script in `docs/mod-makers/asset-workflow.md`.
   - Place the raw bundle file (no extension) at `game/Mods/crabtya.bundle-test/bundles/test-assets`.
2. **Enable `crabtya.bundle-test`** in the Mods menu (toggle shows `(restart)`), then restart.
3. **Verify success path** in `game/BepInEx/LogOutput.log`:
   - `AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test'. Assets=…`
   - `BundleTest: bundle retrieved (key=crabtya.bundle-test:bundles/test-assets).`
   - `BundleTest: PASS - Texture2D 'test_texture' loaded successfully. Size=…`
4. **Verify failure paths**:
   - Remove/rename the bundle file → expect `bundle file not found` warning; other mods load fine.
   - Replace with a text file → expect `LoadFromFile returned null` warning; other mods load fine.
   - Toggle `crabtya.bundle-test` on/off → expect `(restart)` button, command queued to `startup-commands.json`.
5. Confirm `game/BepInEx/ErrorLog.log` stays empty.
6. Once runtime evidence captured, update job-09 and PLAN.md to mark complete.

## Session Summary (2026-05-13) - Job 03 Closure

User confirmed the latest runtime pass is good and approved sign-off ("All good now, sign off the job").

**What changed:**

- `PLAN.md`:
  - Job 03 status moved to `Done (2026-05-13)`.
  - Current master status updated to reflect completed hardening scope and user-confirmed zoom-out stutter mitigation.
  - Recommended next task set to Job 09 (`content.assetBundles` end-to-end proof).
- `docs/development/jobs/job-03-full-code-review.md`:
  - status updated to complete.
  - closure note added with runtime/user validation confirmation.

**Build result:** not rerun (docs/status-only closure pass).

## Session Summary (2026-05-13) - Job 03 Full Code Review (Hardening Pass 2: Runtime Scan Cost)

Continued Job 03 with a focused performance hardening pass on high-frequency scene scans identified in Pass 1.

**What changed:**

- `loader/EIC.ModLoader/CameraPatchApplicator.cs`
  - added camera cache (`CachedCameras`) with periodic full refresh (`CameraRescanIntervalSeconds = 0.75f`) and light null-pruning between refreshes.
  - `ApplyToLiveCameras()` now iterates cached cameras rather than running `FindObjectsOfType<Camera>()` every call.
  - cache now also prunes inactive/disabled cameras between refreshes and excludes them at scan time.
  - added per-frame supported cameraPatch cache to avoid repeatedly filtering/materializing patch lists across camera and hook apply paths.
- `loader/EIC.ModLoader/RuntimeOverlay.cs`
  - removed duplicated camera applicator invocation from the 1-second overlay tick; camera apply now runs through `LateUpdate` + camera-related Harmony hook paths only.
- `loader/EIC.ModLoader/UiScaleApplicator.cs`
  - added eligible-root cache (`CachedEligibleRoots`) with transition-aware rescan cadence:
    - transition-sensitive windows: near-frame rescan cadence (`TransitionBurstTickSeconds`);
    - non-`1x`: moderated eligibility rescan cadence (`0.12s`);
    - `1x`: slower eligibility rescan cadence (`0.30s`).
  - cache is cleared on full baseline restore/reset.
  - cached roots now prune inactive hierarchy objects between scans so menu transitions refresh eligibility sooner.
  - throttled repeated root-binding info logs (minimum interval) to reduce transition-time log spam and associated I/O overhead.
- Docs updated:
  - `docs/development/jobs/job-03-full-code-review.md` status notes now include perf hardening pass 2 and updated remaining focus.
  - `docs/development/test-checklist.md` marks static perf hardening proofs and marks the `Input.GetKeyDown` exception check complete based on latest `LogOutput.log` scan (no matches found).

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` - 0 errors, 0 warnings.

**Log evidence check:**

- `game/BepInEx/LogOutput.log` scan for `Input.GetKeyDown` returned no matches in the latest captured log.
- `game/BepInEx/ErrorLog.log` tail was empty in the latest local check.
- Latest log evidence also shows pause-menu `MOD SETTINGS` injection and successful dedicated Mods window open from the pause path; checklist item updated accordingly.

**Residual follow-up for Job 03 closure:**

1. Runtime QA pass to confirm no regressions in:
   - camera patch responsiveness during gameplay/menu transitions;
   - uiScale responsiveness during pause/main-menu/quit-confirm transitions at non-`1x`.
2. Check `game/BepInEx/ErrorLog.log` remains empty during the above run.
3. Close remaining Job 03 checklist items for Mods window/toggle/runtime validation where evidence is still pending.

## Session Summary (2026-05-13) - Job 03 Full Code Review (Hardening Pass 1)

Continued the next `PLAN.md` primary Job (**Job 03 full code review**) with a static audit and low-risk code hardening changes. This pass focused on correctness and determinism in startup/load behavior plus safer DLL/API boundary behavior.

**What changed:**

- `loader/EIC.ModLoader/ModDiscovery.cs`
  - made mod-folder discovery deterministic by sorting `Directory.EnumerateDirectories(modsRoot)` before manifest scan.
  - made dependency topo processing deterministic by sorting adjacency lists and initial zero-indegree queue.
  - sorted unresolved-cycle IDs before emitting cycle errors.
- `loader/EIC.ModLoader/DllModLoader.cs`
  - narrowed entrypoint type resolution: fallback lookup now only inspects assemblies that physically resolve under the current mod folder path (instead of searching every loaded AppDomain assembly indiscriminately).
  - reduces risk of cross-mod type-name collisions incorrectly binding one mod's entrypoint to another mod assembly.
- `loader/EIC.ModLoader/ContentPipeline.cs`
  - deduplicates identical content warning strings per mod-state entry during apply passes to prevent warning bloat across repeated hot-toggle re-apply operations.
- `loader/EIC.ModLoader/AssetBundleApplicator.cs`
  - normalized AssetBundle cache keys (`\\`/`/` tolerant) via `BuildBundleKey(...)` so `GetBundle(modId, relativePath)` is resilient to slash-style differences.
- Job/docs updates:
  - `docs/development/jobs/job-03-full-code-review.md` now includes an in-progress status update for this hardening pass.
  - `docs/development/test-checklist.md` includes static-proof checklist lines for these hardening changes.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` - 0 errors, 0 warnings.

**Prioritized residual findings (not fixed in this pass):**

1. **[P1 Perf] Camera patch full-scene scan runs in `LateUpdate` path.**
   - `loader/EIC.ModLoader/RuntimeOverlay.cs:84` calls `CameraPatchApplicator.ApplyToLiveCameras()` every frame.
   - `loader/EIC.ModLoader/CameraPatchApplicator.cs:90` performs `FindObjectsOfType<Camera>()` per call.
   - Risk: avoidable frame-time overhead in dense scenes; consider camera cache/invalidation or throttled scene scan + per-camera update.
2. **[P2 Perf] UI scale collector does multiple global `FindObjectsOfType` scans at high cadence when non-`1x`.**
   - `loader/EIC.ModLoader/UiScaleApplicator.cs:290`, `:317`, `:355`, `:393` each scan scene-wide types (`Selectable`, `Text`, `TMP_Text`, `Graphic`) inside `CollectEligibleRoots()`.
   - `CollectEligibleRoots()` is called from apply flow (`loader/EIC.ModLoader/UiScaleApplicator.cs:155`) with non-default cadence as fast as `~0.05s`.
   - Risk: periodic spikes on UI-heavy scenes; next pass should consider root caching, dirty flags, or phased scan schedule.

## Session Summary (2026-05-13) - Job 07 Closure

User confirmed runtime validation is complete ("Perfect, all working."). This pass was closure-only and did not add new runtime/code behavior.

**What changed:**

- `PLAN.md`:
  - Job 07 moved from in-progress to done (`Done (2026-05-13)`) in the Job Board.
  - Current master status now marks Job 07 complete and runtime-validated.
  - Recommended next task updated to Job 03 full code review.
- `docs/development/jobs/job-07-ui-scale-pop-polish.md`:
  - status updated from in progress to complete.
  - runtime QA/evidence capture note updated to complete with validated transition scenarios.
- `docs/development/test-checklist.md`:
  - checked off Job 07 ui-scale verification items (cycle alignment, adaptive tick behavior, pause/quit transition pop checks, quit-confirm popup first-frame check).
  - checked off main-menu `MODS` button size parity at non-`1x`.

**Build result:** not rerun (docs/status-only closure pass).

## Session Summary (2026-05-13) - Job 07 Quit Popup + Mods Button Scale Follow-up

User runtime feedback after the transition-burst pass: large improvement, with only minor remaining popping on quit confirmation popup and an oversized main-menu `MODS` button under uiScale. This pass targeted those two symptoms.

**What changed:**

- `loader/EIC.ModLoader/UiScaleApplicator.cs`:
  - added `IsTransitionSensitivePopupActive()` (checks active `GenericDecisionPopup`) and keeps near-frame cadence (`~0.016s`) while that popup is active.
  - retained prior transition-burst behavior for root-selection changes.
  - added helper `GetCapturedOrCurrentLocalScale(RectTransform)` so non-uiScale systems can request baseline/native scale when available.

- `loader/EIC.ModLoader/MainMenuModsButtonApplicator.cs`:
  - when cloning the native settings button, clone local scale now comes from `UiScaleApplicator.GetCapturedOrCurrentLocalScale(templateRect)` rather than raw `templateRect.localScale`.
  - prevents an oversized Crabtya `MODS` clone when the template is already transformed by active uiScale at clone time.

- `loader/EIC.ModLoader/PauseMenuModsButtonApplicator.cs`:
  - same baseline-aware clone-scale behavior for both side-button and fallback insertion paths.

- Docs/checklists updated for this follow-up:
  - `docs/development/test-checklist.md` adds explicit checks for:
    - quit-confirmation popup first-frame pop at non-`1x`;
    - main-menu `MODS` button size parity with neighboring native entries at non-`1x`.
  - `docs/development/jobs/job-07-ui-scale-pop-polish.md` status update now includes quit-popup transition handling and baseline-scale clone fix.
  - `docs/mod-makers/manifest-format.md` and `game/Mods/faniel.ui-scale/README.md` now mention quit-popup transition sensitivity and baseline-scale menu-button cloning behavior.
  - `PLAN.md` Job 07 status wording updated to include quit-confirm flow and baseline-scale cloning.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Runtime verification still required (next pass):**

- At non-`1x` (`0.75x` and `1.15x`):
  - open pause, select quit, observe `GenericDecisionPopup` open/close for first-frame pop;
  - compare main-menu `MODS` button size against `SETTINGS`/`NEW RUN`/`QUIT` neighboring buttons;
  - repeat gameplay -> pause -> quit-to-main loop several times.
- Confirm `game/BepInEx/ErrorLog.log` remains empty.

## Session Summary (2026-05-13) - Job 07 UI Scale Transition Burst Follow-up

User runtime feedback after the prior Job 07 pass: major improvement, with minor remaining popping mainly when opening pause and quitting back to main menu. This pass replaced dropped-root grace restore logic with transition-burst cadence to smooth those handoffs.

**What changed:**

- `loader/EIC.ModLoader/UiScaleApplicator.cs`:
  - added transition burst cadence constants:
    - `TransitionBurstTickSeconds = 0.016f`
    - `TransitionBurstDurationSeconds = 0.8f`
  - added `_transitionBurstUntilSeconds` tracking and updated `GetRecommendedUpdateIntervalSeconds()`:
    - `~0.25s` while at `1x`;
    - `~0.05s` while non-`1x` normally;
    - `~0.016s` during short transition-burst windows.
  - burst windows now trigger when:
    - eligible-root selection fingerprint changes;
    - eligibility drops to none after a previously-bound state.
  - replaced delayed missing-root restore/removal strategy with lightweight pruning of invalid baselines only (`PruneDroppedRoots`), avoiding explicit restore-on-drop cycles during menu teardown/rebuild.
  - retained baseline restore behavior for active roots and full reset behavior in `RestoreAllTrackedRoots()`.

- Docs updates aligned to current behavior:
  - `PLAN.md` Job 07 status wording now references transition-burst stabilization.
  - `docs/development/jobs/job-07-ui-scale-pop-polish.md` status notes now describe transition-aware burst cadence for pause/main-menu transitions.
  - `docs/development/test-checklist.md` Job 07 transition check now targets pause/open + quit-to-main-menu visible pop under transition-burst behavior.
  - `docs/mod-makers/manifest-format.md` `uiScale` section now documents transition-burst cadence on menu-root changes.
  - `game/Mods/faniel.ui-scale/README.md` runtime notes now describe brief near-frame refresh during major menu transitions.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Runtime verification still required (next pass):**

- Re-test at non-`1x` scale (`0.75x` and `1.15x` minimum):
  - gameplay -> pause open/close repeatedly;
  - gameplay -> quit to main menu;
  - main menu -> level select -> gameplay -> pause -> quit loop.
- Confirm residual pop is reduced and within acceptable native transition noise.
- Confirm `game/BepInEx/ErrorLog.log` remains empty.
- If acceptable, mark Job 07 runtime checklist items complete and close Job 07 in `PLAN.md`.

## Session Summary (2026-05-13) - Job 11 Crabtya Lite Planning

Expanded the new `TODO.md` idea for **Crabtya Lite** into a post-v1 Job at the end of the current plan flow. This was a docs/planning-only pass; no loader code or runtime files changed.

**What changed:**

- `TODO.md` now describes Crabtya Lite as a separate curated QoL-only package for the main game install, with initial built-in scroll zoom/invert scroll/FOV settings and no arbitrary user mod loading.
- Added `docs/development/jobs/job-11-crabtya-lite.md` with objective, scope, architecture decision gate, implementation plan, deliverables, verification, and completion criteria.
- Updated `PLAN.md` to add Job 11 after Job 02, keeping Jobs 10 and 02 as the end of the full Crabtya v1 release/publication flow and making Lite explicitly post-v1.
- Updated `docs/development/jobs/README.md`, `docs/README.md`, and `AGENTS.md` so the new Job is discoverable.

**Important boundary for future agents:**

- Crabtya Lite may omit isolated saves and achievement blocking only while it remains curated QoL-only and does not load user mods, manifests, DLL entrypoints, or declarative content.
- If Lite grows beyond that, pause and reassess against full Crabtya's safety model instead of creating a less-safe full mod loader.

## Session Summary (2026-05-13) - Job 07 UI Scale Pop Polish (Code + Docs Pass)

Continued Job 07 from `PLAN.md` after Job 06 confirmation. This pass focused on targeted code-side anti-pop behavior and documentation polish; runtime QA evidence is still pending.

**What changed:**

- `loader/EIC.ModLoader/UiScaleApplicator.cs`:
  - added adaptive update cadence helper `GetRecommendedUpdateIntervalSeconds()`:
    - `~0.05s` while current UI scale is non-`1x`;
    - `~0.25s` while at `1x`.
  - replaced immediate dropped-root restore with delayed restore tracking:
    - tracks missing eligible roots by instance id (`MissingRootSinceSeconds`);
    - restores baseline/removes cache only after `MissingRootRestoreDelaySeconds` (`0.35s`) to avoid transition snap-back while menus are rebuilding.
  - added `RestoreAllTrackedRoots()` and call sites when `uiScale` is absent/disabled so tracked roots are reset cleanly if the definition disappears.
  - removed stale unused helper paths/fields left from prior strategies (explicit container promotion and pivot/position helpers) to reduce maintenance noise.
- `loader/EIC.ModLoader/RuntimeOverlay.cs`:
  - `Update()` now reads `UiScaleApplicator.GetRecommendedUpdateIntervalSeconds()` and schedules `ApplyToLiveUi()` with adaptive cadence instead of a fixed interval.
- `game/Mods/faniel.ui-scale/README.md`:
  - expanded with recommended ranges, runtime behavior notes, known transition caveat wording, and explicit reset-to-`1.0x` guidance (including optional `runtime-settings.json` key removal while game is closed).
- `docs/mod-makers/manifest-format.md`:
  - updated `uiScale` narrative to document adaptive refresh + dropped-root grace-window behavior.
- `docs/development/test-checklist.md`:
  - added Job 07 verification items for adaptive tick behavior and transition grace-window snap-back checks.
- `docs/development/jobs/job-07-ui-scale-pop-polish.md`:
  - added a dated status update noting code/docs progress and that runtime QA evidence remains open.
- `PLAN.md`:
  - marked Job 07 as **In progress (2026-05-13)** and updated master-status next action to continue Job 07 runtime QA/evidence capture.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Runtime verification still required (next pass):**

- Run full Job 07 UI-scale QA at `0.5x`, `0.75x`, `1x`, `1.15x`.
- Validate main menu, level select (including reward/progression surfaces), gameplay HUD, pause menu, and Mods settings interactions for:
  - no cumulative drift;
  - reduced delayed snap/popping;
  - expected behavior through repeated open/close transitions.
- Verify `game/BepInEx/ErrorLog.log` remains empty.
- Capture/update final evidence in `docs/development/test-checklist.md` and close Job 07 status when confirmed.

## Session Summary (2026-05-13) - Job 06 Scroll Zoom Invert Setting

Added a data-driven `Invert Scroll` setting for any wheel-driven cameraPatch. Implemented as a generic surface on `CameraPatchSettings`, not hard-coded to `faniel.mousewheel-zoom`.

**What changed:**

- `loader/EIC.ModLoader/ContentDefinitions.cs`: added `CameraPatchSettings.Invert` (default `false`, JSON property `invert`).
- `loader/EIC.ModLoader/MouseWheelZoomApplicator.cs`: after computing wheel direction, flips sign when `IsInverted(patch)` is true. The runtime value comes from `RuntimeModSettings.TryGetSettingBool($"{patch.Id}.invert")` and falls back to `patch.Settings.Invert` when no user value is persisted. Exposed `GetInvertSettingKey(patch)` so the settings window uses the same key.
- `loader/EIC.ModLoader/CrabtyaModsSettingsWindow.cs`: added a per-mod `mouseWheelInvertByMod` lookup keyed on `Settings.MouseWheel`, and `CreateMouseWheelInvertRow` renders an `Invert Scroll` ON/OFF toggle inside the mod block. The toggle reuses the existing bool-setting button binding and persists via `RuntimeModSettings.SetSettingBool`.
- `game/Mods/faniel.mousewheel-zoom/defs/mousewheel-zoom.json`: documented default `"invert": false`.
- `game/Mods/faniel.mousewheel-zoom/README.md`: documented the new toggle.
- `docs/mod-makers/manifest-format.md`: documented `settings.invert` and the runtime override key (`<patch.id>.invert`).

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings. Plugin redeployed to `game/BepInEx/plugins/EIC.ModLoader.dll`.

**Runtime verification still required (next session):**

- Enable `faniel.mousewheel-zoom`, open Mods settings, confirm an `Invert Scroll` row appears under the mod block.
- Enter gameplay, scroll up/down: confirm zoom direction matches the toggle state.
- Toggle invert and confirm the change is live without restart.
- Confirm `game/Mods/runtime-settings.json` contains a `faniel.mousewheel-zoom.gameplay-fov.invert` bool entry after toggling.
- Confirm `game/BepInEx/ErrorLog.log` stays empty.

**Notes:**

- The mod is content-only and hot-toggle eligible, so no startup-commands entry should be queued just to flip invert.
- The setting key uses `<patch.id>.invert` so any future wheel-driven cameraPatch gets the same UI/persistence pattern automatically.

## Session Summary (2026-05-13) - Job 01 Codebase Separation Audit

Completed Job 01 from `PLAN.md` as a read-only audit. No code changes were required; the loader/API/sample boundaries are clean.

**Audit results:**

- `rg "faniel\.|persist\.test|test\.visual-targets|SampleDll"` against `loader/EIC.ModLoader/` and `loader/Crabtya.ModApi/`: no matches in core loader or public API sources. The only hits in `loader/` are inside `loader/SampleDllMod/` itself (its own assembly name, type, and build-output deploy path to `game/Mods/faniel.dll-sample/`), which is expected sample ownership.
- `rg "faniel|mousewheel-zoom|ui-scale|dll-sample|visual-targets|persist\.test"` against `loader/EIC.ModLoader/*.cs`: hits are surface descriptors (`"ui.scale"` target, `"UI-scale definition"` log strings) and the `MouseWheelZoomApplicator` class name. No hard-coded mod ids.
- `MouseWheelZoomApplicator.cs` is keyed on `CameraPatchDefinition.Settings.MouseWheel` plus the declarative target `camera.orthographicSize` — fully data-driven, not specific to `faniel.mousewheel-zoom`.
- `UiScaleApplicator.cs` is keyed on the declarative `ui.scale` target — not specific to `faniel.ui-scale`.
- `loader/Crabtya.ModApi/`: no Unity, BepInEx, `EIC.ModLoader`, or sample-mod references — confirmed game-agnostic public contract.
- `loader/EIC.ModLoader/EIC.ModLoader.csproj`: project references are `Crabtya.ModApi` plus game-interop assemblies only; no reference to `SampleDllMod`.
- `loader/SampleDllMod/SampleDllMod.csproj`: references `Crabtya.ModApi` only and deploys its DLL to `game/Mods/faniel.dll-sample/bin/`. Sample is a consumer of the public contract, not a loader runtime dependency.
- `tools/package-release.ps1`: loader package is staged exclusively from loader-owned files (`BepInEx/`, `dotnet/`, doorstop files, `BepInEx/plugins/EIC.ModLoader.dll`, `BepInEx/plugins/Crabtya.ModApi.dll`, `Install-Crabtya.ps1`, `Uninstall-Crabtya.ps1`, docs, templates). Mod packages are staged independently from `game/Mods/<mod-id>/`. No cross-staging.

**What changed:**

- Added an `Ownership Boundaries` section to `docs/development/architecture.md` and added `loader/SampleDllMod/` + `loader/SmokeTestPlugin/` rows to the Component Ownership table so future contributors can tell where loader code ends and mod code begins.
- Updated `PLAN.md` to mark Job 01 status as Done.

**No code/runtime changes were made in this session.** Findings were trivial enough that a dedicated `docs/development/codebase-separation-audit.md` was not warranted; the audit lives here.

**Suggested next task:**

Begin Job 06 from `PLAN.md` (scroll-zoom invert setting) per the prioritized order.

## Session Summary (2026-05-13) - Job Board Reprioritized

This session reprioritized the `PLAN.md` Job board by ease of implementation, importance, dependency risk, and release readiness.

**What changed:**

- Updated `PLAN.md` so the recommended execution order is now:
  1. Job 01 - codebase separation audit
  2. Job 06 - scroll zoom invert setting
  3. Job 07 - UI scale popping/polish
  4. Job 03 - full code review
  5. Job 04 - loader branding
  6. Job 05 - mod API/conflict handling
  7. Job 02 - Git/GitHub publication readiness
- Kept Job IDs tied to the original `TODO.md` paragraphs while making sequence explicit in the board.
- Added parallelism rules to `PLAN.md`, including safe/disallowed overlapping work scopes.
- Updated `docs/development/jobs/README.md` to mirror the recommended order.
- Updated `AGENTS.md` so agents treat `PLAN.md` as the authoritative Job sequence and only parallelize when the plan marks it safe.
- Moved Git publication/repo polish to the final execution slot, while requiring every earlier Job to keep docs, package boundaries, and ownership clean so publication is easier later.

**No code/runtime changes were made in this session.**

**Suggested next task:**

Begin Job 01 from `PLAN.md`. After that, take the quick user-facing Job 06, then Job 07.

## Session Summary (2026-05-13) - PLAN Master Job Board

This session updated `PLAN.md` so it serves as the master document for working through the seven `TODO.md`-derived Jobs.

**What changed:**

- Reframed `PLAN.md` as `Crabtya Master Plan`.
- Added top-level working rules, current master status, a Job board, suggested Job order, and per-Job working notes.
- Preserved the authoritative v1 product direction, runtime contract, supported surfaces, DLL contract, and validation checklist below the Job board.
- Expanded the validation checklist to include the pause/start-menu `MOD SETTINGS` entry, content-only hot-toggle behavior, and current UI scale/settings expectations.
- Updated `docs/development/jobs/README.md` to point back to `PLAN.md` as the master Job board.

**No code/runtime changes were made in this session.**

**Suggested next task:**

Use `PLAN.md` as the entry point, then begin Job 01: `docs/development/jobs/job-01-codebase-separation.md`.

## Session Summary (2026-05-13) - TODO Job Plan Expansion

This session converted the `TODO.md` idea dump into one implementation plan file per paragraph under `docs/development/jobs/`.

**What changed:**

- Added `docs/development/jobs/README.md` as the index and sequencing guide.
- Added seven Job plans:
  - `job-01-codebase-separation.md`
  - `job-02-git-initial-readiness.md`
  - `job-03-full-code-review.md`
  - `job-04-loader-branding.md`
  - `job-05-mod-api-conflicts.md`
  - `job-06-scroll-zoom-invert-setting.md`
  - `job-07-ui-scale-pop-polish.md`
- Updated `AGENTS.md` with Job-plan workflow rules, loader/mod ownership guidance, public Git readiness cautions, review expectations, conflict-handling constraints, branding constraints, and data-driven sample-mod guidance.
- Updated `AGENTS.md` to align supported surfaces with current `PLAN.md`: `uiScale` is active, and `content.assetBundles` are startup-loaded/restart-required.
- Updated `docs/README.md` so the Job plans are discoverable from the documentation index and the current UI direction reflects the dedicated Mods settings window.

**No code/runtime changes were made in this session.**

**Suggested next task:**

Start with `docs/development/jobs/job-01-codebase-separation.md`, because it de-risks the later public GitHub and full-review Jobs.

## Session Summary (2026-05-13) - UI Scale Shrink-Only Presentation Policy + Runtime QA

This session refined `uiScale` after user feedback that reward/progression UI should still be affected because it is large and useful to shrink. The desired behavior is now: broad presentation UI can shrink, but should not grow beyond native size at max scale.

**What changed:**

- Re-enabled reward/progression/pressure UI targeting through a constrained presentation-graphic path in `loader/EIC.ModLoader/UiScaleApplicator.cs`.
- Added per-element scale policy:
  - normal controls follow the configured slider;
  - reward/progression/pressure presentation paths shrink below `1x` but cap at native `1x` when slider is above `1x`;
  - other large UI elements have conservative growth caps based on screen-area ratio.
- Kept Crabtya injected native buttons (`Crabtya.MainMenu.ModsButton`, `Crabtya.PauseMenu.ModsButton`) eligible for scaling.
- Tuned the sample `faniel.ui-scale` range to `0.5`-`1.15` with `0.05` steps.
- Reset `game/Mods/runtime-settings.json` to neutral `1x` after verification so test state is not left enabled.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` 0 errors, 0 warnings.

**Runtime QA performed:**

- Launched the game in windowed mode with `-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -timestamps`.
- Captured screenshots:
  - `ui-scale-title-075.png`
  - `ui-scale-main-menu-075-v2.png`
  - `ui-scale-levelselect-075-v2.png`
  - `ui-scale-main-menu-115.png`
  - `ui-scale-levelselect-115.png`
  - `ui-scale-gameplay-115.png`
  - `ui-scale-pause-115.png` (run ended while navigating, but still confirmed large gameplay/end UI remained bounded)
- Visual result: main menu, injected `MODS` button, level-select rewards/progression, and gameplay HUD stayed aligned; reward/progression shrank at `0.75x` and did not grow at `1.15x`.
- `game/BepInEx/ErrorLog.log` stayed empty during this QA pass.

**Residual follow-up:**

- A cleaner pause-menu screenshot at `0.75x` would still be useful, because the run ended before a normal pause capture.

---

## Session Summary (2026-05-13) - UI Scale Lower Bound + Pop Reduction

This session made two small follow-up fixes:

- Lowered `faniel.ui-scale` minimum from `0.75` to `0.5` so users can make UI elements significantly smaller.
- Reduced menu-size popping by:
  - moving `UiScaleApplicator.ApplyToLiveUi()` from the shared 1-second runtime tick to a short 0.05-second UI-scale tick in `RuntimeOverlay.cs`;
  - removing the delayed first-scale gate, which visibly left newly opened menu elements at native scale before resizing;
  - changing baseline restore to reset only `localScale`, not `anchoredPosition3D` or `pivot`, so Crabtya no longer fights the game's own menu/layout animations.

Runtime verification performed after this follow-up:

- Launched at `0.5x` in windowed mode.
- Captured `ui-scale-main-menu-050.png` and `ui-scale-levelselect-050.png`.
- Visual result: main menu and level-select UI appeared already scaled and aligned; no delayed snap was observed between opening and follow-up screenshots.
- `game/BepInEx/ErrorLog.log` stayed empty.

---

## Session Summary (2026-05-13) - UI Scale Target Tightening

This session followed up on remaining UI-scale issues where some menu/game presentation elements were still being targeted, menus could pop while opening, and Crabtya-injected native buttons were skipped by the scale pass.

**What changed:**

- Tightened `loader/EIC.ModLoader/UiScaleApplicator.cs` target collection by removing the generic `Graphic` probe. This avoids catching level-select/reward/progression art and other non-control presentation pieces just because they render through Unity UI graphics.
- Added explicit presentation-path exclusions for level-select/selective-pressure/reward surfaces.
- Added a short stability gate for newly discovered RectTransforms before capturing their baseline, reducing bad baseline capture during menu-open/layout animation.
- Allowed Crabtya's cloned native buttons (`Crabtya.MainMenu.ModsButton` and `Crabtya.PauseMenu.ModsButton`) through the scaler while continuing to exclude the loader badge and dedicated Mods settings window.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` 0 errors, 0 warnings. The first attempt failed deployment because `Everything is Crab.exe` had the plugin DLL locked; after the process exited, the retry succeeded and deployed `game/BepInEx/plugins/EIC.ModLoader.dll`.

**Runtime verification still required (next session):**

- Main menu and pause menu: verify native buttons and Crabtya-injected `MODS`/`MOD SETTINGS` buttons scale together.
- Level select: verify pressure/reward/progression map art is no longer resized by UI scale.
- Open/close menus repeatedly at non-`1x` scale and check for reduced popping, no layout drift, and no `ErrorLog.log` entries.

---

## Session Summary (2026-05-13) - UI Scale Layout Preservation Fix

This session addressed screenshots showing `uiScale` still corrupting the main menu and in-run UI: menu columns/pause panels shifted off-screen, right-side buttons clipped, and the pause menu drifted/cropped when scale moved away from `1x`.

**What changed:**

- Reworked `loader/EIC.ModLoader/UiScaleApplicator.cs` so it no longer promotes buttons, text, or graphics into parent layout containers before scaling.
- Removed the explicit RectTransform container scan from the eligible-root collection path; broad containers such as `Buttons`, pause panels, and menu columns are no longer direct scale targets.
- Stopped mutating pivots during scale application. Each eligible control is restored to its captured baseline pose and only `localScale` is changed, preserving the game's original anchored position and pivot.
- Kept the existing baseline restore/dropped-root cleanup so scale changes remain reversible when scenes or UI roots change.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` 0 errors, 0 warnings. The build target deployed `game/BepInEx/plugins/EIC.ModLoader.dll`.

**Runtime verification still required (next session):**

- Main menu: move UI Scale to min/max and back to `1x`; verify all native buttons remain in their original layout positions and only their visual/control size changes.
- Pause/in-run HUD: repeat min/max/`1x`; verify pause panel, report button, HUD bars, and bottom ability buttons do not drift or clip.
- Confirm `game/BepInEx/ErrorLog.log` remains empty during repeated slider changes.

---

## Session Summary (2026-05-13) - UI Scale Reversibility + Drift Fix

This session addressed a regression where repeatedly moving the `uiScale` slider (up/down, including back to `1x`) left menu and in-run UI elements offset or permanently oversized.

**What changed:**

- Reworked `loader/EIC.ModLoader/UiScaleApplicator.cs` to make scaling deterministic per tick from a captured baseline pose (`localScale`, `pivot`, and `anchoredPosition3D`) instead of mutating from already-mutated state.
- Added stale-baseline protection by validating the cached `RectTransform` reference and instance ID before reuse.
- Added dropped-root restoration so when root selection changes between ticks/scenes, previously scaled roots are reset to their original baseline before being removed from active scaling.
- Updated pivot preservation to mutate `anchoredPosition3D` (RectTransform space) instead of `localPosition`, reducing anchor drift and off-screen creep.
- Added descendant-root cleanup restore in the dedupe path, preventing compound scale when a child root is replaced by an ancestor root.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` 0 errors, 0 warnings.

**Runtime verification still required (next session):**

- Main menu: scale down then up then return to `1x`; verify all native menu buttons return to baseline positions.
- Level select / in-run HUD: repeat scale up/down cycles; verify no cumulative drift and no off-screen movement.
- Confirm `game/BepInEx/ErrorLog.log` remains empty during repeated slider changes.

---

## Session Summary (2026-05-12) - UI Scale Fix & Implementation

This session focused on fixing the `uiScale` mutation implementation, which was previously marked as safety-disabled due to presentation/layout corruption when modifying UI RectTransform scaling directly.

**What changed:**

- Fixed `UiScaleApplicator.cs` so it now calculates and preserves an edge-aware pivot before applying a scale. This means UI elements shrink towards the corners/edges instead of floating toward the top-left or breaking auto-layouts, successfully delivering the "screen space saving" requirement.
- Updated `CrabtyaModsSettingsWindow.cs` to render dynamic Mod Settings UI rows for any mod that defines a `uiScale` setting with `slider: true`.
- Linked the slider callbacks directly to `UiScaleApplicator.ApplyToLiveUi()` so adjustments take immediate effect during runtime.
- Updated documentation (`PLAN.md`, `README.md`, `manifest-format.md`, and the sample `ui-scale` mod) to reflect that UI scaling is now stable, active, and fully supported.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` 0 errors, 0 warnings.

---

## Previous Session Summary (2026-05-12) - Crabtya Mods Settings UI Native Reskin

This session applied a targeted visual reskin to `CrabtyaModsSettingsWindow.cs` to make the Mods settings window feel native to the *Everything is Crab* menu style instead of like a dense admin panel.

**What changed:**

- Replaced alternating dense row backgrounds (`_rowBgColor` / `_rowAltBgColor`) with separate, button-like `ModBlock` panels.
- Wrapped each mod's header and nested settings in a transparent-background `ModBlock` container.
- Applied `ApplyPanelSpriteStyle` to `ModBlock` containers and added a chunky 4-pixel border (`AddBorder`) so each mod looks like a standalone menu button.
- Increased paddings and margins:
  - `HeaderRowHeight` increased from 86f to 96f.
  - `SettingRowHeight` increased from 54f to 64f.
  - `ModBlockPadding` increased from 8f to 24f for better separation between mods.
  - Internal `ModBlock` padding of 8f added top and bottom.
- Adjusted color palette (`ApplyThemeFromTemplates`) to guarantee a game-native tan/brown palette regardless of template extraction inconsistencies:
  - Panel: `(0.88, 0.78, 0.60)`
  - Mod Blocks: `(0.78, 0.66, 0.46)`
  - Text: Dark brown `(0.24, 0.16, 0.10)`
  - Borders: Dark brown `(0.24, 0.16, 0.10)`
- Adjusted text sizes across the board for a cleaner "chunky pixel text" feel (Header titles 56pt, Mod names 34pt, Toggle labels 24pt, Info text 24pt, Close button 26pt).
- Made the `innerFrame` invisible to remove unnecessary double-boxing and let the new ModBlocks breathe.
- Made the scrollbar track faint and the viewport background transparent.

No overlay path was changed. No architecture was changed. All v1 behaviors preserved.

**Build result:** `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` 0 errors, 0 warnings.

---

## Previous Session Summary (2026-05-12T16:12)

This session completed v1 validation and packaging tasks:

1. **Added logging to LiveModRegistry.Initialize()**
2. **Fixed test.visual-targets mod**
3. **Fixed package-release.ps1 StrictMode issues**
4. **Rebuilt loader**
5. **Runtime validation**
6. **Packaging**

**Status**: Loader is ready for v1 release. Remaining manual tasks: GUI interaction testing, AssetBundle end-to-end test.

---

## Suggested Next Tasks (Priority Order)

1. **Runtime-verify Job 05** (mod API/conflicts) — enable test conflict mods, open Mods menu, toggle, confirm conflict notices appear/clear without restart. Check `ErrorLog.log` stays empty.
2. **Runtime-verify Job 08** (enemy stat patching) — enable `test.enemy-stat`, start a run, check `LogOutput.log` for `EnemyStatPatchApplicator: patch applied` entries.
3. **Runtime-verify Job 09** (AssetBundle) — requires user to supply Unity 6000.2.15f1 test bundle.
4. **Begin Job 10** (v1 validation, release packaging) — run the full v1 Validation Checklist end to end and produce release zips.

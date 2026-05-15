# Test Checklist

## Loader Bootstrap

- [x] Direct launch via `Everything is Crab.exe` works with loader installed.
- [x] Startup includes `Plugin binary fingerprint` log line.
- [x] Startup branding surface uses `Crabtya`.
- [x] Startup includes `SessionIsolationGuard` install logs with sandbox path.
- [x] Startup includes `AssetBundleApplicator: LoadForMods complete.` log line (Loaded=0 when no bundles declared).
- [x] Startup includes `LiveModRegistry: initialized.` log line.

## Mod Discovery and State

- [x] `Mods/` and `Mods/_disabled/` are auto-created as needed.
- [x] `Mods/mod-state.json` is created/updated on bootstrap.
- [x] Enabled/disabled persisted choices survive restart.
- [x] Manifest validation rejects missing required fields and duplicate IDs.
- [x] Dependency/load-order resolution works for valid manifests.
- [x] Static proof: independent mod discovery/load-order is deterministic (alphabetical directory and dependency queue ordering).
- [x] Safe mode enforces `EffectiveEnabled=false` without mutating `Enabled`.
- [x] CLI + queued startup toggles update persisted state correctly.
- [x] Queue file (`Mods/startup-commands.json`) is consumed and cleared on startup.
- [x] Path safety rejects escaped `assemblies`/`definitions`/`assetBundles`/`catalogs`.
- [x] Mods declaring `content.assetBundles` are marked restart-required on toggle.

## DLL Mod Loading

- [x] Public API assembly exists as `Crabtya.ModApi.dll`.
- [x] DLL manifest contract requires paired `assemblies` + `entrypoints`.
- [x] Static proof: DLL entrypoint type resolution is constrained to assemblies under the mod folder.
- [x] DLL entrypoints implementing `ICrabtyaMod` are invoked via `OnLoad`.
- [x] Throwing/missing DLL entrypoints are isolated.
- [ ] Fresh run proof with `faniel.dll-sample` enabled and settings rows visible in Mods menu still required.

## Declarative Content Vertical Slice

- [x] `persist.test` balance/localization definitions load and apply.
- [x] `faniel.mvp-sample` camera/visual definitions apply when enabled.
- [x] `faniel.no-intro` intro skip behavior confirmed via logs.
- [x] `faniel.fov-slider` values persist in `Mods/runtime-settings.json`.
- [x] Static proof: camera patch live-apply now uses a cached camera set with periodic refresh (avoids per-call global camera scan).
- [x] Static proof: duplicate once-per-second camera apply path removed from runtime overlay tick (camera apply remains on per-frame `LateUpdate`/hook paths).
- [x] `faniel.ui-scale` up/down cycles (including return to `1x`) keep menu + in-run UI aligned with no cumulative offset.
- [x] Verify adaptive uiScale tick behavior in logs/runtime (`~0.05s` while non-`1x`, `~0.25s` when `1x`) with no visible delayed snap after menu open.
- [x] Static proof: uiScale eligibility roots are cached with transition-aware rescan cadence (reduces repeated global UI-type scans).
- [x] Verify brief pause/open and quit-to-main-menu transitions do not show visible scale snap/pop beyond native animation noise at non-`1x` scale (transition-burst behavior).
- [x] Verify quit confirmation popup (`GenericDecisionPopup`) opens/closes at non-`1x` with no noticeable one-frame size pop on first frame.
- [ ] Gameplay-visible proof for balance hook effect after latest UI changes still pending.

## Hot-Toggle (Live Mod Apply)

- [ ] Toggling a content-only mod (e.g. `faniel.fov-slider`) applies immediately: label shows `ENABLED`/`DISABLED` without `(restart)`, `mod-state.json` updates, `startup-commands.json` is NOT written.
- [ ] Camera/visual effect of a hot-toggled mod is visible within ~1 s without restart.
- [ ] Toggling a DLL mod (e.g. `faniel.dll-sample`) queues to `startup-commands.json` and shows `(restart)` label.
- [x] Toggling a DLL mod (e.g. `faniel.dll-sample`) queues to `startup-commands.json` and shows `(restart)` label.
- [ ] Hint text updates to "N changes queued for restart…" when restart-required toggles are pending.
- [ ] Toggling a mod back to its original state clears the queue entry; label drops `(restart)`.
- [x] Static proof: repeated content re-apply does not duplicate identical content warning strings per mod-state entry.

## Asset Bundle Loading

- [ ] `content.assetBundles` path validates at manifest load time (file must exist under mod folder).
- [x] `AssetBundleApplicator: loaded bundle '…'` line appears in log for a declared valid bundle.
- [x] Bundle built with wrong Unity version or invalid payload emits `LoadFromFile returned null` warning and loading continues (no crash).
- [x] `ICrabtyaModContext.AssetBundles.TryGetBundle(relativePath, out _)` returns the loaded bundle to a DLL mod.
- [x] `ICrabtyaModContext.AssetBundles.LoadAllAssets<T>()` / `LoadAsset<T>()` return non-null Unity assets from a loaded bundle.
- [x] Mod declaring bundles shows `(restart)` on toggle in Mods window.
- [x] Static proof: AssetBundle cache keys are slash-normalized for robust `GetBundle` lookups.

## Visual Patch Targets

- [x] `player.baseSprite` — SpriteRenderers on PlayerCharacter replaced by mod PNG.
- [ ] `enemy.baseSprite` — runtime log confirms heuristic fires on at least one live enemy type; sprites replaced.
- [ ] `gameobject.named:<pattern>` — SpriteRenderers on matching GameObject replaced.
- [ ] `sprite.named:<pattern>` — SpriteRenderers whose sprite.name matches pattern replaced.
- [ ] Unsupported visual target is rejected with manifest warning (not a load error).

## UI and Settings (Crabtya v1)

- [x] Main menu shows Crabtya status label.
- [x] Main menu injects native-styled `MODS` entry.
- [x] `MODS` opens dedicated Crabtya native-style Mods settings menu window.
- [x] Native settings row injector is intentionally disabled to avoid split settings surfaces.
- [x] Mods window content area uses ScrollRect + scrollbar — long mod lists scroll.
- [x] Hint text is dynamic: shows "Camera/visual mods apply live…" or pending restart count.
- [x] Verify current build no longer throws repeated `UnityEngine.Input.GetKeyDown` Input System exceptions (latest `LogOutput.log` scan found no matches).
- [ ] Runtime proof: Mods window content scrolls when more than ~8 rows are present.
- [ ] Validate toggle queue/persistence and camera + DLL setting writes from Mods window in one full run.
- [x] In-run pause/start menu shows native-styled `MOD SETTINGS` entry that opens the same dedicated Mods window (no second settings surface). Latest log evidence shows pause-menu button creation plus Mods window open sequence.
- [x] At non-`1x`, main-menu `MODS` button size matches neighboring native entries (no oversized clone from scaled template capture).

## Packaging and Distribution

- [x] Packaging script includes `Crabtya.ModApi.dll` and loader DLL.
- [x] `Install-Crabtya.ps1` and `Uninstall-Crabtya.ps1` scripts created and bundled in loader zip.
- [x] Mod templates (`content-mod-template`, `dll-mod-template`) in `templates/` and bundled in loader zip.
- [x] `PLAN.md` and docs aligned to current implementation.
- [x] Final `package-release.ps1` run produces complete zip with scripts, templates, and docs.
- [ ] `Install-Crabtya.ps1` tested against a clean game directory — all checklist items pass.
- [ ] `Uninstall-Crabtya.ps1` tested — game starts vanilla after uninstall.

## Safety

- [ ] Explicit regression proof that original game binaries/assets remain untouched is still pending.
- [ ] `game/BepInEx/ErrorLog.log` should remain empty in the next full validation run.
- [ ] Runtime proof that achievement unlock/stat write calls are blocked in a modded run (blocked-call log lines require a gameplay run that triggers an achievement write).
- [x] Runtime proof that save reads/writes land under `game/CrabtyaData/IsolatedSave/` and base save files are not mutated (confirmed in earlier session; re-validate with current build).

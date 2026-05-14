# Crabtya Master Plan

`PLAN.md` is the master working document for Crabtya. Use it to decide what to work on next, then open the linked Job plan for the detailed implementation steps.

## How To Work This Plan

- Work one primary Job at a time unless this plan marks work as safe to parallelize and the write scopes stay disjoint.
- Start every Job by reading this file, the linked Job plan, `AGENTS.md`, `docs/development/architecture.md`, and `docs/development/agent-handoff.md`.
- Keep the v1 product/runtime contract below intact while completing Jobs.
- After meaningful work, update the relevant Job plan if scope/status changed, update `docs/development/agent-handoff.md`, and update user/mod-maker/development docs touched by the change.
- If code changes, build with `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- If runtime behavior changes, verify with `game/BepInEx/LogOutput.log`, `game/BepInEx/ErrorLog.log`, `game/Mods/mod-state.json`, `game/Mods/startup-commands.json`, and `game/Mods/runtime-settings.json` as relevant.
- Do not modify original game binaries or bundled assets under `game/Everything is Crab_Data/`, the base executable, or base DLLs.

## Current Master Status

- Crabtya v1 core loader exists and is the active product direction.
- Detailed Job plans expanded from earlier `TODO.md` ideas (Jobs 01-07), previously-untracked PLAN follow-ups (Jobs 08-10), and the current Crabtya Lite TODO note (Job 11) live under `docs/development/jobs/`.
- Job 01 (codebase separation audit) is complete; loader/API/sample boundaries are clean and the ownership boundaries are documented in `docs/development/architecture.md`.
- Job 06 (scroll-zoom invert setting) is complete and runtime-verified; `Invert Scroll` toggle renders in the Mods menu for wheel-driven camera patches, persists to `runtime-settings.json`, and flips zoom direction live.
- Job 07 (UI scale pop polish) is complete (2026-05-13); adaptive ui-scale refresh cadence, transition-burst stabilization for pause/main-menu/quit-confirm flows, and baseline-scale cloning for Crabtya menu buttons are landed and runtime-validated.
- Job 03 (full code review) is complete (2026-05-13): static and runtime hardening landed (deterministic discovery/load-order, mod-local DLL entrypoint resolution, warning dedupe, AssetBundle key normalization, camera/ui scan-caching, per-frame camera patch cache, duplicate camera-apply removal, and reduced uiScale log churn), with user-confirmed minor zoomed-out stutter mitigation.
- Job 08 (enemy stat patching surface) is complete and runtime-verified (2026-05-13); `EnemyStatPatchApplicator` patches confirmed in `LogOutput.log` across multiple enemies and archetypes.
- Job 10 (v1 validation, release packaging) is in progress (2026-05-13): release packaging complete; 10 of 15 checklist items confirmed from runtime logs (fingerprint, content-only load, badge+menu UI, settings persist, hot-apply, ErrorLog empty, achievement patching, IsolatedSave redirect, conflict detection, enemy stat patches). Remaining 5 items (DLL-only load, hybrid load, broken-mod isolation, throwing-entrypoint isolation, startup-commands queue) require a run with DLL mods enabled.
- Job 02 (git publication) is complete (2026-05-14): `.gitignore`, `.gitattributes`, root README, packages README all in place; initial commit `60c04e4` (142 files); remote origin set to `https://github.com/nofaniel/Crabtya.git`; `master` pushed to origin at `cf378d5`. Remaining publication work is release-page/tag publishing, not repo initialization.
- Git publication/repo polish is intentionally last for the Crabtya v1 release flow, but every earlier v1 Job should leave docs, package boundaries, and source ownership cleaner so Job 02 is mostly final assembly rather than archaeology. GitHub repo setup is now complete; remaining public-facing polish is release-page work and later optional GitHub Pages presentation.
- Brand/logo assets are available at repo root as `logo-L.png`, `logo-M.png`, and `logo-S.png`; Jobs touching branding, README/GitHub presentation, package polish, or screenshots should prefer these files over older draft assets.
- The intended private GitHub remote is `https://github.com/nofaniel/Crabtya.git`. Repo initialization and push are complete; remaining GitHub publication work is release-page/tag creation and later optional presentation polish.
- Job 11 (Crabtya Lite) is a post-v1 follow-up and must not expand the v1 release gate.

## Job Board

Recommended order is based on importance, implementation ease, dependency risk, and release readiness. Job IDs stay stable once assigned; the sequence below is the order agents should tackle them.

| Seq | Job | Status | Priority | Effort | Parallel Lane | Detailed Plan | Purpose | Completion Signal |
|---|---|---|---|---|---|---|---|---|
| 1 | 01 | Done (2026-05-13) | Critical | Low/Medium | Foundation | `docs/development/jobs/job-01-codebase-separation.md` | Verify loader/API code is cleanly separated from currently developed mods and sample behavior. | Separation audit complete; ownership boundaries documented in `docs/development/architecture.md`; no code changes required. |
| 2 | 06 | Done (2026-05-13) | High | Low | Focused Mod Polish | `docs/development/jobs/job-06-scroll-zoom-invert-setting.md` | Add a persisted invert-scroll setting to the Mouse Wheel Zoom mod. | `Invert Scroll` toggle in Mods menu, persists to `runtime-settings.json`, flips wheel direction at runtime. |
| 3 | 07 | Done (2026-05-13) | High | Medium | Focused Mod Polish | `docs/development/jobs/job-07-ui-scale-pop-polish.md` | Fix remaining UI scale popping and polish the UI Scale sample mod. | Runtime proof across menus/scenes with no cumulative drift and clear docs. |
| 4 | 03 | Done (2026-05-13) | Critical | High | Review/Hardening | `docs/development/jobs/job-03-full-code-review.md` | Review loader, menus, public API, bundled mods, templates, tools, and docs for bugs/performance/polish. | Findings/fixes recorded with verification and prioritized residual risks. |
| 5 | 09 | In progress (2026-05-14, bundle loading proven; asset extraction blocked by BepInEx/Unity 6 IL2CPP vtable gap) | High | Medium | Surface Verification | `docs/development/jobs/job-09-assetbundle-proof.md` | Prove `content.assetBundles` end to end with a real Unity 6000.2.15f1 bundle and document the mod-maker build recipe. | Bundle loads + asset retrieves + visible effect, with success and failure paths logged. |
| 6 | 04 | Done (2026-05-13) | Medium/High | Medium | UX Polish | `docs/development/jobs/job-04-loader-branding.md` | Improve the small red Crabtya loaded label into a stronger native-feeling branding surface. | Runtime-proven branding with safe fallback and no old overlay revival. |
| 7 | 05 | Done (2026-05-13) | Critical | High | API/Loader Evolution | `docs/development/jobs/job-05-mod-api-conflicts.md` | Harden the mod API and add conflict notification/prevention for conflicts Crabtya can prove. | Conflict metadata/detection/UI/docs work with unrelated mods still loading. |
| 8 | 08 | Done (2026-05-13) | High | Medium | API/Loader Evolution | `docs/development/jobs/job-08-enemy-stat-patching.md` | Add `enemy.stat.*` declarative balance-patch surface after one runtime probe pass to confirm class/method names. | Enemy stat moves at runtime via manifest, validation isolates bad entries, docs updated. |
| 9 | 10 | In progress (2026-05-13, runtime validation pending) | Critical | Medium/High | Release Gate | `docs/development/jobs/job-10-v1-validation-release-packaging.md` | Run the full v1 Validation Checklist end to end and produce the final loader/template release zips under `packages/`. | All 15 checklist items have captured evidence; versioned release artifacts staged. |
| 10 | 02 | Done (2026-05-14) | High | Medium | Publication | `docs/development/jobs/job-02-git-initial-readiness.md` | Prepare repo for initial git/GitHub publication with README, ignore rules, and release clarity. | Root README and `.gitignore` ready; initial commit 60c04e4 (142 files); remote origin set to https://github.com/nofaniel/Crabtya.git; `master` pushed to origin at `cf378d5`. Release-page publishing remains. |
| 11 | 11 | Not started | Medium/High | High | Post-v1 Product Split | `docs/development/jobs/job-11-crabtya-lite.md` | Build a separate minimal QoL-only Crabtya Lite package with built-in native settings and no arbitrary mod loading. | Lite architecture is separated from full Crabtya; scroll/FOV QoL settings work on the main install without full-loader surfaces. |

## Job Dependencies, Parallelism, And Suggested Order

1. Do Job 01 first. It is the foundation check and should stay mostly read-only unless it finds real crossover.
2. Do Job 06 next. It is a small, user-visible improvement and a good test of the settings pipeline after the separation audit.
3. Do Job 07 after Job 06. It is more important to user trust than branding, and it should leave the UI scale sample release-ready.
4. Do Job 03 after the focused mod polish. The full review should inspect the latest code, not a pre-polish snapshot.
5. Do Job 09 after Job 03. AssetBundle loading already exists in code; verifying it end to end with a real Unity 6000.2.15f1 bundle catches surface gaps before later Jobs depend on it. Depends on the user supplying or building the test bundle.
6. Do Job 04 after Job 09 unless the user supplies final brand art earlier. Branding is visible and valuable, but it should not outrank correctness, menu stability, or unverified content surfaces.
7. Do Job 05 after Jobs 01, 03, and the focused polish Jobs. Conflict handling is high-impact but larger and should build on the cleaned-up loader/API surface.
8. Do Job 08 after Job 05. Adding the `enemy.stat.*` surface is another declarative content extension; doing it after API/conflict hardening lets it reuse the validation and conflict patterns instead of competing with them. The runtime probe step can be scheduled earlier if Job 03 has spare time.
9. Do Job 10 second to last in the v1 flow. The full v1 validation pass and release packaging should run against the final feature set so the captured evidence matches what ships.
10. Do Job 02 last in the v1 flow. Git publication and repo polish should benefit from all previous docs/code cleanup, then package it coherently for public readers.
11. Do Job 11 only after the v1 flow is complete. Crabtya Lite is a separate post-v1 product split, not a late addition to the full v1 loader.

Safe parallelism rules:

- Job 01 can run in parallel only with read-only planning for other Jobs; do not make overlapping code edits until its separation findings are known.
- Job 06 and Job 07 may run in parallel only if different agents own disjoint files. Job 06 owns mouse-wheel camera/settings files and `faniel.mousewheel-zoom`; Job 07 owns UI scale files and `faniel.ui-scale`. Coordinate shared files such as `CrabtyaModsSettingsWindow.cs`, `RuntimeModSettings.cs`, `ContentDefinitions.cs`, and docs before editing.
- Job 04 brand asset/design exploration may run in parallel with Job 03 review if it is read-only or asset-only. Code integration for branding should wait until review findings about menu/bootstrap code are known.
- Job 09 can run in parallel with Job 03 read-only review, and the Unity-side bundle build can run in parallel with any earlier Job. Loader-side changes to `AssetBundleApplicator.cs` or `Crabtya.ModApi` retrieval helpers should serialize against Job 03 and Job 05 if they touch the same files.
- Job 05 should not run in parallel with Jobs 03, 06, 07, 08, or 09 if it changes manifest models, discovery, settings, state, or Mods menu rendering. It is too central; let the earlier work settle first.
- Job 08 must not run in parallel with Job 05 once Job 05 begins schema/validation work, since both touch `ContentDefinitions`, manifest validation, and applicator wiring. The Job 08 runtime probe pass is read-only and safe to schedule earlier.
- Job 10 is verification + packaging only and should run alone. Do not start new feature work in parallel; any fixes it forces should be small, scoped, and recorded in the Job 10 plan.
- Job 02 can have lightweight README outline notes collected throughout, but final `.gitignore`, publication README, and package-facing polish should happen last.
- Job 11 can have product notes collected earlier, but implementation should wait until Jobs 10 and 02 are done. If it shares code with full Crabtya, extract/reuse only narrowly scoped QoL applicators and serialize any edits to shared loader files.
- If two Jobs need the same file, stop parallel work and serialize the change. A calm queue beats merge soup.

## Per-Job Working Notes

### Job 01 - Codebase Separation

Keep generic loader behavior in `loader/EIC.ModLoader/` and public contracts in `loader/Crabtya.ModApi/`. Keep sample or mod-specific behavior in `loader/SampleDllMod/`, `game/Mods/<mod-id>/`, and `templates/`. Search for sample IDs or personal mod IDs in core loader code and either remove hard-coded coupling or document why the reference is intentional.

### Job 02 - Git Initial Readiness

Create a public-facing root README, establish ignore rules, and document what is source versus local game/runtime/generated state. Do not delete local game files or generated artifacts without explicit user confirmation; use `.gitignore`, packaging, and README guidance first.

Use the available logo files (`logo-L.png`, `logo-M.png`, `logo-S.png`) for README/GitHub presentation, release-page imagery, and any repo social preview guidance. The target private GitHub repository is `https://github.com/nofaniel/Crabtya.git`; repo setup is complete, so remaining work here is release-page clarity and later optional GitHub Pages/social-preview polish unless the user redirects.

### Job 03 - Full Code Review

Review with a bug-finding mindset: startup, discovery, manifest validation, path safety, dependency resolution, content application, DLL entrypoint isolation, settings persistence, menu UI, runtime scans, sample mods, templates, packaging, and docs. Fix safe issues directly and record risky/larger issues with file/line references.

### Job 04 - Loader Branding

Improve the visible Crabtya loaded branding while staying narrow: main-menu status only, native-feeling, compact, no raycast blocking, graceful fallback if an asset is missing, and no return of the old runtime overlay panel/debug controls.

If branding is revisited, prefer the finalized root logo files (`logo-L.png`, `logo-M.png`, `logo-S.png`) for loader-owned brand treatment, docs screenshots, packaging, and GitHub-facing assets. Keep any runtime asset copy under a loader-owned/package-owned path rather than original game assets.

### Job 05 - Mod API And Conflicts

Make Crabtya easier for mod makers while avoiding false promises. Prefer explicit conflict/dependency metadata and deterministic declarative conflict checks. Block only conflicts Crabtya can prove; warn clearly for softer overlaps; keep unrelated valid mods loading.

### Job 06 - Scroll Zoom Invert

Add invert-scroll as a data-driven setting for wheel-driven camera patches, not as a hard-coded special case for `faniel.mousewheel-zoom`. Preserve current behavior by default and persist the setting through `game/Mods/runtime-settings.json`.

### Job 07 - UI Scale Pop Polish

Verify current behavior first, then fix remaining popping/layout drift with targeted baseline/eligibility/timing changes. Preserve game-owned layout and animations. Document any unavoidable limitations honestly.

### Job 08 - Enemy Stat Patching Surface — Done (2026-05-13, runtime verification pending)

Code complete and built (0 errors, 0 warnings). Added `EnemyStatPatchApplicator.cs` with a Harmony postfix on `BasicEnemyCharacter.InitEnemyInstanceStatsIfNeeded`, confirmed via interop DLL inspection. Supports `enemy.stat.*` (global) and `enemy.<EEnemyArchetype>.stat.*` (per-archetype) target prefixes with the same set/add/multiply vocabulary as player stat patches. No changes to `ContentPipeline.cs` or `ContentDefinitions.cs` — balance patches route through existing infrastructure. Test fixture at `game/Mods/test.enemy-stat/`. Docs updated: `architecture.md`, `manifest-format.md`, `agent-handoff.md`. Runtime verification pending: enable `test.enemy-stat` and check `LogOutput.log` for apply lines.

### Job 09 - AssetBundle End-to-End Proof

Verify the existing `content.assetBundles` loading path against a real Unity 6000.2.15f1 StandaloneWindows64 bundle. Coordinate with the user to obtain or build the test bundle; consume it through a minimal hybrid test mod that proves load, retrieval via `AssetBundleApplicator.GetBundle`, and a visible in-game effect. Exercise missing/corrupt/misdeclared failure paths and document the mod-maker build recipe.

### Job 10 - v1 Validation Pass And Release Packaging

Run the full v1 Validation Checklist below against the latest build, fix only what the checklist forces, and stage the final loader and template release zips under `packages/` at a single coherent version string. This is the release gate before Job 02 publication; treat new feature work as out of scope here.

### Job 11 - Crabtya Lite QoL Loader

Create a separate post-v1 Lite package for curated QoL-only features: scroll zoom/invert scroll and FOV in native-feeling settings. Lite must not load user mods, manifests, DLL entrypoints, or full Crabtya state/UI, and it must not weaken the full loader's save-isolation/achievement-blocking contract. Prefer separation by project/package over a runtime mode flag.

## Product Direction (Authoritative)

Crabtya v1 is a folder-based mod loader for *Everything is Crab* where players install mods by dropping folders into `game/Mods/<mod-id>/`.

The v1 UX is:

- a small main-menu `Crabtya loaded v<version>` status label;
- a native-styled `MODS` button inserted into the main menu;
- an in-run pause/start-menu `MOD SETTINGS` entry that opens the same settings surface;
- a dedicated loader-owned native-style Mods settings menu window opened from those buttons.

The v1 runtime safety boundary is:

- modded sessions must not unlock Steam achievements;
- modded sessions must not read/write the base game's primary save-space;
- Crabtya owns an isolated modded save-space rooted at `game/CrabtyaData/IsolatedSave/`.

Planned follow-up now tracked on the Job board:

- Enemy stat patching surface (`enemy.stat.*` balance patches) - Job 08 (Done, 2026-05-13).
- AssetBundle end-to-end proof against a Unity 6000.2.15f1 test bundle - Job 09.
- Full v1 validation pass and release packaging - Job 10.
- Post-v1 Crabtya Lite QoL loader - Job 11.

Post-v1 product split:

- Crabtya Lite is planned after full Crabtya v1 publication. It is a curated QoL-only package for the main install, not a folder-based user mod loader.
- Lite may omit isolated saves and achievement blocking only while it excludes arbitrary user mods/content/DLL entrypoints and stays limited to built-in QoL settings.
- If Lite grows beyond curated QoL settings, it must be reassessed against full Crabtya's modded-session safety model.

Recently landed:

- Hot-toggle for content-only mods - declarative-only mods with no assemblies, catalogs, or asset bundles now apply instantly without restart; `LiveModRegistry` drives the re-apply.
- Mods window scrollable layout - `ScrollRect` plus `Scrollbar` added; hint text is dynamic.
- Installer/uninstaller scripts - `tools/Install-Crabtya.ps1` and `tools/Uninstall-Crabtya.ps1` bundled in the loader package.
- Mod templates - `templates/content-mod-template/` and `templates/dll-mod-template/` with full READMEs.
- AssetBundle loading — `AssetBundleApplicator` loads `content.assetBundles` at startup before DLL entrypoints; end-to-end infrastructure proven with `crabtya.bundle-test` hybrid test mod (runtime success-path verification pending user supplying a Unity 6000.2.15f1 bundle); full Unity build recipe documented in `docs/mod-makers/asset-workflow.md`.
- Extended visual targets - `visual` definitions support `player.baseSprite`, `enemy.baseSprite`, `gameobject.named:<name>`, and `sprite.named:<name>`.
- In-run start/pause menu hosts a `MOD SETTINGS` entry that opens the same dedicated Crabtya Mods settings window with no duplicate settings surface.
- Mods window frame definition and typography refreshed for stronger pixel-art contrast.

That dedicated menu is the authoritative settings surface for v1 and contains:

- mod enable/disable toggles - live for content-only mods; queued for next launch for DLL/catalog/bundle mods;
- camera slider settings from declarative `cameraPatch` entries that declare slider metadata;
- DLL-registered settings from `Crabtya.ModApi` (`bool`, `int`, `float`, option/string);
- UI scale settings from declarative `uiScale` entries that declare slider metadata.

## Core Runtime Contract

- Manifest path remains `game/Mods/<mod-id>/eicmod.json`.
- Discovery/state ownership remains:
  - `game/Mods/`
  - `game/Mods/_disabled/`
  - `game/Mods/mod-state.json`
- Startup toggle queue remains `game/Mods/startup-commands.json` and is consumed on startup.
- Runtime setting persistence remains `game/Mods/runtime-settings.json`.
- Base-game save IO in modded sessions is redirected into `game/CrabtyaData/IsolatedSave/`.
- Steam achievement/stat write paths are blocked in modded sessions.
- Safe mode remains `--eic-safe-mode` or `eic-safe-mode.flag`.
- All declared file paths for a mod must remain relative to that mod folder and cannot escape it.

## Supported v1 Mod Surfaces

Active declarative surfaces:

- `localization`
- `balancePatch`
- `cameraPatch`
- `visual` - targets: `player.baseSprite`, `enemy.baseSprite`, `gameobject.named:<name>`, `sprite.named:<name>`
- `introSkip`
- `content.assetBundles` - Unity AssetBundle files loaded at startup before DLL entrypoints; restart-required when toggled
- `uiScale`

Future-facing / discovery-only:

- `evolution`
- `enemy` - the `enemy.stat.*` slice is on the Job board as Job 08; the rest of this surface remains discovery-only.

Do not assume future-facing surfaces are live unless code and runtime verification confirm them.

## DLL Mod Contract

DLL mods must declare both:

- `assemblies`: relative DLL paths inside the mod folder;
- `entrypoints`: fully qualified types implementing `Crabtya.ModApi.ICrabtyaMod`.

Crabtya loads enabled mods in resolved dependency order, applies declarative content, then loads/invokes DLL entrypoints.
Failure isolation is mandatory: one bad mod must not block other valid mods.

## v1 Validation Checklist

For each meaningful implementation pass, capture runtime proof in `game/BepInEx/LogOutput.log` and ensure:

1. Plugin fingerprint line proves deployed DLL provenance.
2. Valid content-only mod loads.
3. Valid DLL-only mod loads.
4. Valid hybrid mod loads.
5. Broken manifest or missing DLL is isolated.
6. Throwing DLL entrypoint is isolated.
7. Main menu shows Crabtya label and injected native-styled `MODS` button.
8. `MODS` opens the dedicated native-style Mods settings menu.
9. Pause/start menu `MOD SETTINGS` opens the same dedicated Mods settings menu.
10. Settings interactions persist to `game/Mods/runtime-settings.json`.
11. Enable/disable toggles queue to `game/Mods/startup-commands.json` and apply next launch when restart-required.
12. Content-only toggles hot-apply without writing a restart command.
13. `game/BepInEx/ErrorLog.log` remains empty for the validated run.
14. Achievement unlock attempts are blocked with no Steam achievement writes from a modded run.
15. Save files touched by the run are created under `game/CrabtyaData/IsolatedSave/` and do not mutate the base save location.

## Source Documents

- `TODO.md` - original idea dump that produced the current Job board.
- `docs/development/jobs/README.md` - detailed Job index.
- `docs/development/jobs/job-01-codebase-separation.md` through `job-11-crabtya-lite.md` - implementation plans for each Job.
- `AGENTS.md` - agent operating rules and repo constraints.
- `docs/development/architecture.md` - runtime architecture and component ownership.
- `docs/development/test-checklist.md` - detailed verification matrix.
- `docs/development/agent-handoff.md` - latest session status and residual risks.

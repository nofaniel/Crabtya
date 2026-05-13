# AGENTS.md

## Scope
- This repo is a Windows game install plus a .NET 6 BepInEx IL2CPP loader project, not a normal library/app workspace.
- The active implementation target is the Crabtya v1 plan in `PLAN.md`; use it to guide product decisions and task priorities.
- Treat `docs/` as the maintained source of truth for loader status and handoff context, but when older docs conflict with `PLAN.md` or executable sources, follow `PLAN.md`, loader code, and runtime evidence.

## Product Direction
- Build the loader as **Crabtya**, the folder-based mod loader for *Everything is Crab*.
- Users install mods by dropping folders into `game/Mods/<mod-id>/`.
- Each mod folder uses `eicmod.json` as its manifest.
- v1 must support both declarative content mods and managed DLL entrypoint mods.
- Do not ship the old runtime overlay mods panel/debug controls in v1.
- v1 loader-owned UX must stay narrow and native-feeling:
  - a small native-looking `Crabtya` loaded/version label on the main menu;
  - a native-styled `MODS` main-menu button;
  - a dedicated native-style Crabtya Mods settings menu opened from that button.
- Mod settings should be surfaced through that dedicated native-style Mods settings menu (not the removed overlay panel).
- Crabtya Lite is a post-v1 follow-up, not part of the v1 release gate: it should be a separate curated QoL-only package with no arbitrary user mod loading and no weakening of full Crabtya's safety model.

## Active Job Plans
- Expanded implementation plans for the current `TODO.md` idea dump live in `docs/development/jobs/`.
- Use `PLAN.md` as the master Job board; its sequence is authoritative even though Job file numbers preserve the original `TODO.md` paragraph order.
- Work one primary Job at a time unless `PLAN.md` marks a parallel lane as safe and write scopes stay disjoint.
- Treat each assigned TODO/PLAN idea as a separate Job; do not merge unrelated Jobs into a single implementation pass.
- Before starting a Job, read its plan file plus `PLAN.md`, `docs/development/architecture.md`, and `docs/development/agent-handoff.md`.
- When finishing meaningful Job work, update the Job file if scope/status changes, update `docs/development/agent-handoff.md`, and update the relevant user/mod-maker/development docs in the same pass.
- Current Job plan files:
  - `docs/development/jobs/job-01-codebase-separation.md`
  - `docs/development/jobs/job-02-git-initial-readiness.md`
  - `docs/development/jobs/job-03-full-code-review.md`
  - `docs/development/jobs/job-04-loader-branding.md`
  - `docs/development/jobs/job-05-mod-api-conflicts.md`
  - `docs/development/jobs/job-06-scroll-zoom-invert-setting.md`
  - `docs/development/jobs/job-07-ui-scale-pop-polish.md`
  - `docs/development/jobs/job-08-enemy-stat-patching.md`
  - `docs/development/jobs/job-09-assetbundle-proof.md`
  - `docs/development/jobs/job-10-v1-validation-release-packaging.md`
  - `docs/development/jobs/job-11-crabtya-lite.md`

## Where To Work
- Loader source currently lives in `loader/EIC.ModLoader/`; keep using that path unless a rename is clearly necessary and safe.
- If/when Job 11 starts, prefer a separate `loader/Crabtya.Lite/` project or clearly separated package path for Lite instead of hiding Lite behind a runtime flag in the full loader.
- Public DLL mod API source lives in `loader/Crabtya.ModApi/`; keep it generic and free of sample-mod-specific behavior.
- Sample DLL mod source lives in `loader/SampleDllMod/`; do not let sample-only behavior become a runtime dependency of the loader.
- The smoke-test plugin in `loader/SmokeTestPlugin/` is only for BepInEx compatibility validation.
- Runtime artifacts are deployed into the checked-in game install under `game/`, especially `game/BepInEx/plugins/EIC.ModLoader.dll`.
- You may add or replace loader/plugin files, docs, sample mod content, and supporting config/state under `game/Mods/`.
- Do not modify original game binaries or bundled assets under `game/Everything is Crab_Data/` or the base executable/DLLs.

## Build And Run
- Build the loader with `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- The csproj uses direct file references into `game/BepInEx/core/` and `game/BepInEx/interop/`; builds will fail if that local BepInEx install is missing or mismatched.
- Launch the game through `game\__LAUNCHER.bat` or `game\Everything is Crab.exe -timestamps` so BepInEx writes timestamped logs.
- After building, verify the deployed plugin is the one the game loaded by checking the startup `Plugin binary fingerprint` log line in `game/BepInEx/LogOutput.log`.
- If deployment fails because `game/BepInEx/plugins/EIC.ModLoader.dll` is locked, close the game and retry.

## Verification
- Primary verification is runtime, not unit tests.
- Use `game/BepInEx/LogOutput.log`, `game/BepInEx/ErrorLog.log`, `game/Mods/mod-state.json`, `game/Mods/startup-commands.json`, and `game/Mods/runtime-settings.json`.
- `ErrorLog.log` staying empty is a meaningful success signal in the current workflow.
- There is no solution file, test project, lint config, or formatter config checked in at repo root.
- For v1 work, verify all of the following whenever relevant:
  - a valid content-only mod loads;
  - a valid DLL-only mod loads;
  - a valid hybrid DLL + content mod loads;
  - a broken manifest or missing DLL is isolated safely;
  - a throwing DLL entrypoint does not block other mods;
  - the old runtime Mods overlay panel does not appear;
  - main menu shows small `Crabtya` status label and injected `MODS` button;
  - `MODS` opens the dedicated native-style Mods settings menu;
  - mod toggles/settings in that menu persist and survive restart when applicable.

## Runtime Behavior That Matters
- Loader bootstrap entrypoint is `loader/EIC.ModLoader/Plugin.cs`.
- Discovery must manage `game/Mods/`, `game/Mods/_disabled/`, and `game/Mods/mod-state.json`.
- Safe mode is triggered by `--eic-safe-mode` or a marker file named `eic-safe-mode.flag` beside the game executable unless intentionally changed with backward-compatible aliases.
- Startup mod toggles `--eic-enable-mod=<id>` and `--eic-disable-mod=<id>` may remain as developer/admin tools; queued next-launch toggles are read from `game/Mods/startup-commands.json` and consumed on startup.
- Manifest file remains `game/Mods/<mod-id>/eicmod.json`.
- DLL mod manifests must support explicit relative `assemblies` and `entrypoints` fields.
- Add and preserve a public API surface for DLL mods via `Crabtya.ModApi.dll`, including `ICrabtyaMod`, `ICrabtyaModContext`, logging, settings registration, and lifecycle support.
- v1 mod loading should scan every folder under `game/Mods/` except `_disabled`, validate manifests, resolve load order/dependencies, apply declarative content for enabled mods, then load declared assemblies and invoke declared entrypoints for enabled DLL mods.
- Broken mods must fail in isolation; one bad mod must not block other valid mods from loading.
- All DLL, asset, catalog, and definition paths must remain relative to the mod folder and may not escape it.

## Supported v1 Mod Surfaces
- Keep/apply these declarative definition types in v1:
  - `localization`
  - `balancePatch`
  - `cameraPatch`
  - `visual`
  - `introSkip`
  - `uiScale`
- `content.assetBundles` are loaded at startup before DLL entrypoints and remain restart-required when toggled.
- `evolution` and `enemy` are future-facing surfaces unless they gain runtime proof and implementation.
- Do not assume previously recognized types are live unless code and runtime verification confirm them.

## UI And Settings Constraints
- Remove or disable loader-owned mod menu behavior from the old runtime overlay path; do not preserve that overlay as a user-facing v1 feature.
- Reuse old overlay/bootstrap code only when it directly helps with main-menu branding, native-styled `MODS` button insertion, or the dedicated Mods settings window.
- Add a Crabtya-owned settings registry for mods.
- Mods should register settings during `OnLoad`.
- v1 settings types are `bool`, `int`, `float`, and enum/string option.
- Persist loader-owned settings under `game/Mods/`, likely in `runtime-settings.json` or per-mod config files.
- Dedicated Mods settings rendering must not replace or corrupt the game's own settings storage.
- If dedicated menu rendering fails, log clearly and continue loading mods without crashing the game.

## Repo Conventions
- Keep `docs/development/agent-handoff.md` updated after every meaningful milestone.
- Preserve the docs-first workflow: if code or runtime behavior changes, update the relevant docs in `docs/development/`, `docs/mod-makers/`, or `docs/users/` as part of the same task unless the current user explicitly limits the edit scope.
- Rebrand user-facing docs, logs, package names, and status strings toward `Crabtya`, while keeping risky internal path renames optional until they are safe.
- When docs and older prose disagree, trust executable sources, persisted state files, runtime logs, and the current `PLAN.md`.
- Keep loader-vs-mod ownership clean: generic support belongs in `loader/EIC.ModLoader/` or `loader/Crabtya.ModApi/`; sample behavior belongs in `loader/SampleDllMod/`, `game/Mods/<mod-id>/`, or `templates/`.
- For public Git/GitHub readiness work, avoid deleting local game files or generated artifacts without explicit user confirmation; prefer `.gitignore`, README guidance, and release packaging docs first.
- For review work, report concrete bugs/risks with file and line references, fix safe issues directly, and leave larger risks prioritized in handoff docs.
- For conflict-handling work, block only conflicts Crabtya can prove; warn clearly for softer overlaps and keep unrelated valid mods loading.
- For branding work, keep the UX narrow and native-feeling; do not revive the old runtime overlay panel or debug controls.
- For `faniel.mousewheel-zoom` and `faniel.ui-scale` work, keep behavior data-driven through manifests/settings rather than hard-coding sample mod IDs into generic loader paths.

## High-Value References
- `PLAN.md`: current Crabtya v1 implementation plan and product boundaries.
- `docs/development/jobs/README.md`: index of current one-at-a-time implementation Jobs expanded from `TODO.md`.
- `docs/development/architecture.md`: runtime flow and file ownership; update it toward the Crabtya loader-first architecture as implementation changes land.
- `docs/development/test-checklist.md`: what is actually verified vs still pending.
- `docs/development/agent-handoff.md`: latest milestone status, known blockers, and next action.
- `docs/investigation/loader-compatibility.md`: confirmed BepInEx IL2CPP smoke-test pass and expected launch/log artifacts.

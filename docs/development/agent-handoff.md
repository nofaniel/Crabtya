# Agent Handoff

## Current Snapshot (2026-05-15)

- **All Jobs (01-11) are complete.** Crabtya v1 and Crabtya Lite are both released.
- **No active Jobs remain.** GitHub release-page/tag publishing is complete; next work is any new features added to `PLAN.md` plus optional release-note/presentation polish.
- **Current bundle guidance for mod makers:** use `ICrabtyaModContext.AssetBundles` APIs.
- **Known upstream limitation (documented):** direct Unity wrapper calls `bundle.LoadAsset*` / `bundle.LoadAllAssets*` remain constrained on this runtime by the Unity 6/BepInEx IL2CPP `ReadOnlySpan<T>.GetPinnableReference()` interop gap.

### 2026-05-15 - Release Pages Published + Fresh Packages Regenerated

Status:

- GitHub release/tag publishing complete for:
  - `v1.0.0`
  - `crabtya-lite-v1.0.0`
  - `faniel.dll-sample-v1.0.0`
  - `faniel.fov-slider-v1.0.0`
  - `faniel.mousewheel-zoom-v1.0.0`
  - `faniel.mvp-sample-v1.0.0`
  - `faniel.no-intro-v1.0.0`
  - `faniel.ui-scale-v1.0.0`

Packaging refresh:

- `tools/package-release.ps1 -ReleaseLabel v1.0.0 -Clean -ModId ...` rerun successfully.
- Refreshed Lite artifact: `packages/lite/Crabtya-Lite-v1.0.0.zip` (63,013,425 bytes)
- Refreshed Lite SHA256: `5FA7BDF4C0F35EBE31E69378DA94CA485B3010AE3BE59C418E9A5EE91A796FC5`

## What Landed Recently (Latest First)

### 2026-05-15 - Job 11 Closed: Crabtya Lite Runtime-Verified and Packaged

Status:

- Job 11 is complete.

Runtime evidence (Lite-only install — full EIC.ModLoader absent):

- `game/BepInEx/LogOutput.log` shows: `Loading [Crabtya Lite 0.1.0]`, `Crabtya Lite bootstrap loaded.`, `Crabtya Lite camera applicator installed.`, `Crabtya Lite native settings integration installed.`, `Crabtya Lite runtime initialized.`
- `game/BepInEx/ErrorLog.log` was empty.
- No `game/Mods/` scanning, `mod-state.json`, or full Mods settings window behavior observed.
- Coexistence guard confirmed active: Lite aborts when `EIC.ModLoader.dll` is present (unless `--crabtya-lite-allow-full` override is passed).

Packaging:

- `packages/lite/Crabtya-Lite-v1.0.0.zip` produced (63,013,053 bytes).
- SHA256: `2A568DD7E60765CF7B8438EF35F31E45BA8AA8274920C18291372399C5BF63D5`
- Install/uninstall scripts: `tools/Install-CrabtyaLite.ps1` and `tools/Uninstall-CrabtyaLite.ps1`.
- `package-release.ps1` fix: validation errors on intentional fixture mods no longer block `--SkipMods` runs.

Docs:

- `docs/users/crabtya-lite.md` expanded with full Lite vs full Crabtya comparison table.
- `PLAN.md` Job 11 status updated to Done.



- New project path: `loader/Crabtya.Lite/`
  - `Crabtya.Lite.csproj`
  - `Plugin.cs`
  - `LiteRuntimeBootstrap.cs`
  - `LiteSettingsStore.cs`
  - `LiteCameraApplicator.cs`
  - `LiteNativeSettingsApplicator.cs`
  - `NullableAttributes.cs`
- Lite runtime currently includes:
  - full-loader coexistence detection + default abort/warn behavior;
  - Lite-owned settings persistence (`game/CrabtyaLite/settings.json`);
  - mouse-wheel zoom, invert-scroll, FOV controls via native-feeling settings injection.

Build highlights:

- `dotnet build loader/Crabtya.Lite/Crabtya.Lite.csproj -c Release` passes.
- `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` still passes.

Immediate remaining work for Job 11:

1. Lite-only runtime verification evidence capture.
2. Lite packaging/install/uninstall flow under `packages/lite/`.
3. User docs comparing Lite vs full Crabtya and compatibility guidance.

### 2026-05-14 - Job 09 Closed with Failure-Path Isolation Hardening

Status:

- Job 09 moved to done for v1 scope: startup load, hybrid extraction via mod API, and failure-path isolation are runtime-proven.

Code/runtime highlights:

- `loader/EIC.ModLoader/AssetBundleApplicator.cs`
  - Removed `AssetBundle.LoadFromMemory(byte[])` fallback because corrupt payloads could trigger fatal native `AccessViolationException` in this runtime.
  - Kept safe `LoadFromStream` primary path and `LoadFromFile` diagnostics fallbacks.
- Added deterministic fixtures:
  - `game/Mods/crabtya.bundle-missing/`
  - `game/Mods/crabtya.bundle-corrupt/`

Runtime evidence highlights (`game/BepInEx/LogOutput.log`):

- Missing path isolated at validation (`Referenced assetBundle file is missing...`).
- Corrupt payload isolated at load (`LoadFromStream returned null`, `LoadFromFile returned null`, diagnostics logged).
- Other mods still load in same run (`crabtya.bundle-test`, `faniel.dll-sample`).
- `game/BepInEx/ErrorLog.log` remained empty in validated run.

### 2026-05-14 - Clean Mod-Maker Bundle API Surface

Status:

- DLL mod authors no longer need loader-internal calls for runtime bundle extraction.

Code highlights:

- Added `loader/Crabtya.ModApi/ICrabtyaAssetBundleRegistry.cs`.
- Extended `loader/Crabtya.ModApi/ICrabtyaModContext.cs` with:
  - `ICrabtyaAssetBundleRegistry AssetBundles { get; }`
- Implemented loader-side registry wiring in `loader/EIC.ModLoader/DllModLoader.cs`.
- Updated sample hybrid bundle mod to API-first usage:
  - `game/Mods/crabtya.bundle-test/src/BundleTestEntrypoint.cs`

### 2026-05-14 - Job 10 Closed (15/15)

Status:

- Job 10 validation/release-packaging checklist is fully evidenced and done.

Runtime evidence highlights:

- Startup queue consume/apply path captured (`Queued startup mod-toggle commands consumed`, `Startup toggle applied...`).
- `startup-commands.json` consumed/cleared after launch.
- `mod-state.json` reflected queue-consumed and resolved intent fields.
- `ErrorLog.log` remained empty.

## Documentation Status

Aligned during latest passes:

- `PLAN.md`
- `docs/development/jobs/README.md`
- `docs/development/jobs/job-09-assetbundle-proof.md`
- `docs/development/jobs/job-10-v1-validation-release-packaging.md`
- `docs/development/test-checklist.md`
- `docs/development/v1-signoff-runbook.md`
- `docs/development/architecture.md`
- `docs/mod-makers/asset-workflow.md`
- `docs/mod-makers/manifest-format.md`
- `docs/mod-makers/quickstart.md`
- `templates/dll-mod-template/README.md`

## Residual Notes

- Keep direct-wrapper AssetBundle limitation note in docs until upstream interop fix lands.
- Prefer `context.AssetBundles.TryGetBundle(...)`, `GetAssetNames(...)`, `LoadAsset<T>()`, and `LoadAllAssets<T>()` in mod-maker examples.

## Next Job Kickoff (Job 11 - Crabtya Lite)

Read before coding:

1. `PLAN.md`
2. `docs/development/jobs/job-11-crabtya-lite.md`
3. `docs/development/architecture.md`
4. `AGENTS.md`

Immediate first step:

- Create a short Lite architecture decision doc under `docs/development/` that locks boundaries (separate project/package, no mod discovery/manifests/DLL mod loading, curated QoL-only settings), then scaffold the Lite project path once decision text is approved in-repo.

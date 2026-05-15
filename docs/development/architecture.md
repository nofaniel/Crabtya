# Architecture

## v1 Goals

- Ship Crabtya as a stable folder-based mod loader for IL2CPP builds.
- Keep install/uninstall simple and reversible (PowerShell scripts + manual extraction).
- Support declarative content mods and managed DLL entrypoint mods.
- Keep bad mod failures isolated.
- Expose a native-feeling in-game UX through:
  - small main-menu Crabtya status text;
  - injected main-menu `MODS` button;
  - injected in-run pause/start-menu `MOD SETTINGS` button;
  - dedicated native-style Mods settings menu window (shared between both entry points).

## High-Level Runtime Flow

1. Startup begins in `loader/EIC.ModLoader/Plugin.cs` and logs plugin fingerprint.
2. Startup toggles are loaded from CLI args and `Mods/startup-commands.json` (consumed and deleted).
3. Discovery scans `Mods/`, validates each `eicmod.json`, resolves dependency order.
4. Enabled/effective mods apply declarative content via `ContentPipeline.Apply()`.
5. Enabled mods' declared `content.assetBundles` are loaded via `AssetBundleApplicator.LoadForMods()`.
6. Enabled DLL mods load assemblies and invoke `ICrabtyaMod.OnLoad()` entrypoints.
7. `LiveModRegistry.Initialize()` hands over the live state for hot-toggle support.
8. Session isolation guard redirects base-game save paths and blocks Steam achievement/stat writes.
9. Runtime hooks spawn `RuntimeOverlayBehaviour` which drives per-frame applicators and Crabtya UI:
   - main-menu `Crabtya loaded v…` label
   - main-menu `MODS` button injection
   - in-run pause-menu `MOD SETTINGS` button injection
   - per-tick re-application of camera, visual, intro-skip, UI-scale patches
10. Mods window `Show()` re-runs `Refresh()` to rebuild the mod list for the current live state.

## Component Ownership

| File | Responsibility |
|---|---|
| `loader/EIC.ModLoader/Plugin.cs` | Startup entrypoint; wires all subsystems |
| `loader/EIC.ModLoader/RuntimeHooks.cs` | Harmony hooks for game lifecycle events |
| `loader/EIC.ModLoader/RuntimeOverlay.cs` | Per-frame MonoBehaviour driver; hosts badge label |
| `loader/EIC.ModLoader/SessionIsolationGuard.cs` | Save sandbox + achievement write blocking |
| `loader/EIC.ModLoader/MainMenuModsButtonApplicator.cs` | Injects `MODS` button into main menu |
| `loader/EIC.ModLoader/PauseMenuModsButtonApplicator.cs` | Injects `MOD SETTINGS` into pause menu |
| `loader/EIC.ModLoader/CrabtyaModsSettingsWindow.cs` | Mods settings window; ScrollRect layout; hot-toggle UI |
| `loader/EIC.ModLoader/ModDiscovery.cs` | Manifest scan, validation, load-order resolution, state write |
| `loader/EIC.ModLoader/ModManifest.cs` | JSON model for `eicmod.json` |
| `loader/EIC.ModLoader/ModStateStore.cs` | Read/write `Mods/mod-state.json` |
| `loader/EIC.ModLoader/LiveModRegistry.cs` | Post-bootstrap state cache; hot-toggle for content-only mods |
| `loader/EIC.ModLoader/ContentPipeline.cs` | Applies declarative definitions to runtime state |
| `loader/EIC.ModLoader/AssetBundleApplicator.cs` | Loads `content.assetBundles` at startup; public bundle access API |
| `loader/EIC.ModLoader/BalancePatchApplicator.cs` | Harmony-hooks player stat refresh; applies `player.stat.*` patches |
| `loader/EIC.ModLoader/EnemyStatPatchApplicator.cs` | Harmony-hooks `BasicEnemyCharacter.InitEnemyInstanceStatsIfNeeded`; applies `enemy.stat.*` and `enemy.<archetype>.stat.*` patches |
| `loader/EIC.ModLoader/CameraPatchApplicator.cs` | Per-frame orthographic camera size applicator |
| `loader/EIC.ModLoader/VisualPatchApplicator.cs` | Per-frame sprite replacement; 4 targets (player, enemy, gameobject.named, sprite.named) |
| `loader/EIC.ModLoader/IntroSkipApplicator.cs` | Suppresses intro/splash video playback |
| `loader/EIC.ModLoader/UiScaleApplicator.cs` | UI scale applicator (baseline restore + edge-aware pivot scaling) |
| `loader/EIC.ModLoader/FovSettingsSliderApplicator.cs` | FOV slider in native settings |
| `loader/EIC.ModLoader/MouseWheelZoomApplicator.cs` | Mouse-wheel camera zoom |
| `loader/EIC.ModLoader/LocalizationHooks.cs` | Hooks Unity Localization table lookups |
| `loader/EIC.ModLoader/DllModLoader.cs` | Loads DLL assemblies and invokes `ICrabtyaMod.OnLoad()` |
| `loader/Crabtya.ModApi/` | Public mod API (`ICrabtyaMod`, `ICrabtyaModContext`, settings, logging, bundle access) |
| `loader/EIC.ModLoader/StartupCommands.cs` | Parses CLI mod-toggle args |
| `loader/EIC.ModLoader/StartupCommandQueue.cs` | Read/write/consume `Mods/startup-commands.json` |
| `loader/EIC.ModLoader/SafeMode.cs` | Detects `--eic-safe-mode` flag or marker file |
| `tools/Install-Crabtya.ps1` | PowerShell installer; extracts loader zip into game directory |
| `tools/Uninstall-Crabtya.ps1` | PowerShell uninstaller; removes loader-owned files |
| `tools/package-release.ps1` | Builds loader + mod zips; bundles scripts and templates |
| `templates/content-mod-template/` | Starter template: all declarative definition types |
| `templates/dll-mod-template/` | Starter template: DLL mod with full settings registration |
| `loader/SampleDllMod/` | Sample DLL mod (`faniel.dll-sample`); consumes `Crabtya.ModApi` only; deploys to `game/Mods/faniel.dll-sample/`. Not a runtime dependency of the loader. |
| `loader/SmokeTestPlugin/` | BepInEx IL2CPP smoke-test plugin; not shipped with releases and not a runtime dependency of the loader. |

## Ownership Boundaries

- `loader/EIC.ModLoader/` owns generic loader behavior only. It references `Crabtya.ModApi` and game/BepInEx interop assemblies; it does not reference `SampleDllMod` or any installed mod, and does not hard-code any specific mod id.
- `loader/Crabtya.ModApi/` is the public DLL mod contract. It is game-agnostic — no Unity, BepInEx, EIC.ModLoader, or sample-mod references.
- `loader/SampleDllMod/` is a consumer of `Crabtya.ModApi`. Its build deploys output to `game/Mods/faniel.dll-sample/`; nothing in `loader/EIC.ModLoader/` or `loader/Crabtya.ModApi/` depends on it.
- Sample/personal mod content (`faniel.*`, `persist.test`, `test.visual-targets`, etc.) lives only in `loader/SampleDllMod/` and under `game/Mods/<mod-id>/`. Loader code reaches mod-specific behavior exclusively through manifest fields, content definitions, and runtime settings — never by mod id.
- Loader behavior that may look mod-specific (mouse-wheel zoom, UI scale) is keyed on declarative surface markers (`cameraPatch.settings.mouseWheel`, `uiScale` with target `ui.scale`), not on `faniel.mousewheel-zoom` / `faniel.ui-scale`.
- `tools/package-release.ps1` builds the loader zip from loader-owned files (`BepInEx/`, `dotnet/`, doorstop files, `BepInEx/plugins/EIC.ModLoader.dll`, `BepInEx/plugins/Crabtya.ModApi.dll`, scripts, docs, templates) and emits each mod zip independently from `game/Mods/<mod-id>/`. Loader and mod packages do not share staged content.

## Runtime Data Contract

| Path | Owner | Description |
|---|---|---|
| `Mods/` | Crabtya | Root for all installed mods |
| `Mods/_disabled/` | Crabtya | Optional holding area for manually-disabled mods |
| `Mods/mod-state.json` | Crabtya | Per-mod enabled/effective/error/applied state; written each startup and on hot-toggle |
| `Mods/startup-commands.json` | Crabtya | Next-launch toggle queue; consumed and deleted on startup |
| `Mods/runtime-settings.json` | Crabtya | Persisted DLL-registered and slider setting values |
| `CrabtyaData/IsolatedSave/` | Crabtya | Modded-session save sandbox; base-game save paths are redirected here |

- `Enabled` = persisted user intent. Only flipped by toggle actions or startup queue.
- `EffectiveEnabled` = launch-time activation state. False in safe mode or if manifest errored.
  Also updated immediately by `LiveModRegistry.TryHotToggle()` for content-only mods.
- Steam achievement/stat write APIs are blocked while any mod is loaded.

## Hot-Toggle Model

Content-only mods (no `assemblies`, no `content.catalogs`, no `content.assetBundles`) support
live toggling without restart:

1. `CrabtyaModsSettingsWindow` calls `LiveModRegistry.TryHotToggle(modId, enabled)`.
2. `ContentPipeline.Apply()` is re-run synchronously with the updated mod state.
3. `RuntimeContentState.Set()` updates the shared live content state.
4. `mod-state.json` is updated on disk (read-modify-write).
5. Per-frame applicators pick up the new state within one tick.

Mods with assemblies, catalogs, or asset bundles always require restart — the toggle writes to
`startup-commands.json` and the toggle button shows `(restart)`.

## Settings Model (v1)

- Mods register settings via `ICrabtyaModContext.Settings` during `OnLoad`.
- Supported kinds: `bool`, `int`, `float`, option/string list.
- Persisted in `Mods/runtime-settings.json`.
- Rendered in the dedicated native-style Mods settings menu window (ScrollRect layout).
- `CrabtyaNativeSettingsApplicator` is intentionally disabled to avoid split settings surfaces.

## Visual Patch Target Model

`visual` definitions route to `VisualPatchApplicator.ApplyToLiveObjects()` on the ~1 s per-tick
scan. The `target` field selects which live objects are scanned:

| Target value             | Scan method |
|---|---|
| `player.baseSprite`        | Find `World.Characters.PlayerCharacter` components |
| `enemy.baseSprite`         | Find components whose type name contains Enemy/Creature/Boss/Npc/Monster (excludes PlayerCharacter) |
| `gameobject.named:<name>`  | Find any active component on a GameObject whose `.name` contains `<name>` |
| `sprite.named:<name>`      | Find all live `SpriteRenderer`s whose `sprite.name` contains `<name>` |

The `player.baseSprite` target also fires immediately via a Harmony postfix on
`World.Characters.PlayerCharacter:Start` / `OnEnable` / `SetupInitialStats` for fast response
when the player spawns.

## Asset Bundle Model

`content.assetBundles` paths are loaded during startup (after content pipeline, before DLL mods)
via `AssetBundleApplicator.LoadForMods()`. Bundles must be built with Unity **6000.2.15f1**.
Loaded bundles are cached for the session lifetime and accessible to DLL mods via
`ICrabtyaModContext.AssetBundles`.

The public bundle API exposed to mods is:

- `TryGetBundle(relativePath, out object? bundle)`
- `GetAssetNames(relativePath)`
- `LoadAsset<TAsset>(relativePath, assetPath)`
- `LoadAllAssets<TAsset>(relativePath)`

Internally, these route through loader compat methods that bypass Unity 6 IL2CPP
string-span interop failures seen with direct `bundle.LoadAsset<T>()` /
`bundle.LoadAllAssets()` wrappers.

## Enemy Stat Patch Model

Enemy stat patches use the same `balancePatch` definition type as player stat patches, but with
`enemy.stat.*` or `enemy.<archetype>.stat.*` target prefixes.

**Hook**: Harmony postfix on `World.Characters.Enemies.BasicEnemyCharacter.InitEnemyInstanceStatsIfNeeded`
(confirmed 2026-05-13 via interop DLL inspection).

**Stats class**: `World.Stats.EnemyStats` — resolved from `BasicEnemyCharacter.EntityStats`.

**Archetype**: `EEnemyArchetype` enum — resolved from `BasicEnemyCharacter.EnemyArchetype` (string value
used for per-archetype patch matching, e.g. `"BlobFish"`).

**Target formats**:
- `enemy.stat.<statName>` — applies to every enemy regardless of archetype.
- `enemy.<EEnemyArchetype>.stat.<statName>` — applies only when the enemy's archetype string matches.

**Confirmed `EnemyStats` properties** (via interop DLL 2026-05-13):
`AttackAreaModifier`, `BaseAbilityDamage`, `BasePhysicalDamage`, `CataclysmReductionPercentage`,
`CharmResistance`, `ChaseSpeed`, `ColdAdaptation`, `CooldownModifier`, `FeedingDistance`,
`FeedingSpeed`, `FleeSpeed`, `GeneralDamageMultiplier`, `HeatAdaptation`, `HpRegeneration`,
`MaxHp`, `MaxHpIncreaseFromHpFood`, `MaxPlating`, `MovementSpeed`, `NumExtraPoisonTics`,
`PoisonDamageMultiplier`, `PoisonReductionPercentage`, `PoisonTicksSpeedModifier`,
`SandTerrainAdaptation`, `ShieldedStatusDamageResistance`, `Size`, `SnowTerrainAdaptation`,
`SoakAdaptation`, `SprintRecoveryImpactSpeedMultiplier`, `SprintSpeedMultiplier`,
`StunDurationMultiplier`, `StunReductionPercentage`, `TerrainAdaptation`, `TurningSpeed`,
`WaterTerrainAdaptation`.

**Inheritance chain**: `EnemyStats → AEnemyStats → AEntityStats → Il2CppSystem.Object`

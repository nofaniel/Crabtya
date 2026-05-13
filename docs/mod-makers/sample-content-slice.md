# Sample Content Slice

## Purpose

This page tracks the current sample folder mods used for vertical-slice validation.

## Sample Location

- `game/Mods/persist.test/`
  - `eicmod.json`
  - `README.md`
  - `defs/d1.json` (`balancePatch` sample)
  - `defs/loc.en.json` (`localization` sample)
- `game/Mods/faniel.mvp-sample/`
  - `eicmod.json`
  - `README.md`
  - `defs/camera.json` (`cameraPatch` sample)
  - `defs/player-sprite.json` (`visual` sample)
  - `assets/base-crab.png` (local PNG sprite asset)
- `game/Mods/faniel.fov-slider/`
  - `eicmod.json`
  - `README.md`
  - `defs/fov-slider.json` (`cameraPatch` sample with runtime settings slider)
- `game/Mods/faniel.mousewheel-zoom/`
  - `eicmod.json`
  - `README.md`
  - `defs/mousewheel-zoom.json` (`cameraPatch` sample with mouse wheel live control)
- `game/Mods/faniel.no-intro/`
  - `eicmod.json`
  - `README.md`
  - `defs/no-intro.json` (`introSkip` sample)
- `game/Mods/faniel.ui-scale/`
  - `eicmod.json`
  - `README.md`
  - `defs/ui-scale.json` (`uiScale` sample)
- `game/Mods/faniel.dll-sample/`
  - `eicmod.json`
  - `README.md`
  - `bin/faniel.dll-sample.dll` (managed DLL sample)

## Current Scope

- The loader currently discovers, validates, and applies these definitions into loader-managed runtime registries.
- The sample localization entries now target known live UI keys observed in runtime logs (`UI_Codex`, `UI_Challenges`, `DIFFICULTY_TWO_TITLE`, `EVOLUTION_ATTACK_BASICSTARTING_TITLE`) plus evolution card keys that are visibly active in gameplay (`EVOLUTION_PASSIVE_OUTLIER_TITLE`, `EVOLUTION_PASSIVE_OUTLIER_DESCRIPTION`, `EVOLUTION_PASSIVE_OUTLIER_FUNFACT`) to make in-game override verification explicit.
- The MVP sample mod demonstrates folder-local gameplay changes: `camera.orthographicSize` is multiplied by `1.35`, and `player.baseSprite` is replaced from `assets/base-crab.png`.
- The FOV slider sample mod demonstrates an adjustable `camera.orthographicSize` multiplier. Its default is `1.0`, and native settings row slider changes persist to `Mods/runtime-settings.json`.
- The mouse wheel zoom sample mod demonstrates wheel-driven `camera.orthographicSize` adjustment using `settings.mouseWheel=true`. Values also persist to `Mods/runtime-settings.json`.
- The balance sample currently targets `player.stat.NumberOfStandardEvolutionChoices` and adds `2`, giving the next runtime proof a visible evolution-picker surface instead of a placeholder/nonexistent stat.
- Definition discovery and apply record summary metadata in `Mods/mod-state.json`:
  - `DefinitionFileCount`
  - `DefinitionTypes`
  - `AppliedDefinitionCount`
  - `AppliedDefinitionTypes`
  - `AppliedDefinitionIds`
  - `AppliedLocales`
  - `AppliedLocalizationEntryCount`
  - `AppliedBalancePatchCount`
  - `AppliedCameraPatchCount`
  - `AppliedVisualPatchCount`
- Runtime content application exists for localization, balance patches, camera patches, and the MVP visual sprite replacement path. Live verification on May 10, 2026 confirmed the sample camera patch widened gameplay cameras and the sample PNG replaced the spawned player sprite.
- Runtime content application now also includes intro-video skip (`introSkip`). `uiScale` is supported and scales native UI elements individually safely.
- DLL loading now supports `assemblies` + `entrypoints` with `Crabtya.ModApi` and includes the `faniel.dll-sample` proof mod.

## Validation Expectations

After launch, `Mods/mod-state.json` should show for `persist.test`:

- `DefinitionFileCount: 2`
- `DefinitionTypes` includes:
  - `balancePatch`
  - `localization`
- `AppliedDefinitionCount: 2`
- `AppliedDefinitionTypes` includes:
  - `balancePatch`
  - `localization`
- `AppliedBalancePatchCount: 1`
- `AppliedLocalizationEntryCount: 7`
- Balance apply logs should include `Member=NumberOfStandardEvolutionChoices.FlatValue` once the player stats refresh hook fires.

When enabled after launch, `Mods/mod-state.json` should show for `faniel.mvp-sample`:

- `DefinitionFileCount: 2`
- `DefinitionTypes` includes:
  - `cameraPatch`
  - `visual`
- `AppliedDefinitionCount: 2`
- `AppliedCameraPatchCount: 1`
- `AppliedVisualPatchCount: 1`

When enabled after launch, `Mods/mod-state.json` should show for `faniel.fov-slider`:

- `DefinitionFileCount: 1`
- `DefinitionTypes` includes:
  - `cameraPatch`
- `AppliedDefinitionCount: 1`
- `AppliedCameraPatchCount: 1`

When enabled after launch, `Mods/mod-state.json` should show for `faniel.mousewheel-zoom`:

- `DefinitionFileCount: 1`
- `DefinitionTypes` includes:
  - `cameraPatch`
- `AppliedDefinitionCount: 1`
- `AppliedCameraPatchCount: 1`

## Quick Verify

1. Build and deploy `EIC.ModLoader.dll`.
2. Launch once with `--eic-disable-mod=persist.test` and confirm persisted state.
3. Launch once with `--eic-enable-mod=persist.test` and confirm persisted state.
4. Check `BepInEx/LogOutput.log` for definition discovery and apply summary lines.
5. Open main menu / evolution UI surfaces and confirm overridden text appears for the keys listed above.
6. Start or enter gameplay with `persist.test` enabled, reach an evolution choice, and confirm the standard choice count reflects the `+2` balance patch.
7. Disable `persist.test`, restart, and confirm the standard evolution choice count returns to vanilla.
8. Start or enter gameplay and confirm the MVP sample camera view is wider and the player sprite uses the local PNG asset.
9. Disable `faniel.mvp-sample`, restart, and confirm `AppliedCameraPatches=0` and `AppliedVisualPatches=0`.
10. With `faniel.fov-slider` enabled, open native settings and adjust the `FOV` slider row. Confirm `Mods/runtime-settings.json` records `faniel.fov-slider.gameplay-fov` and camera size changes while in gameplay.
11. With `faniel.mousewheel-zoom` enabled (and `faniel.fov-slider` disabled for isolation), scroll wheel in gameplay and confirm `Mods/runtime-settings.json` records `faniel.mousewheel-zoom.gameplay-fov` and live camera changes.
12. Enable `faniel.dll-sample` and confirm log line `DLL mod 'faniel.dll-sample' entrypoint loaded` plus persisted `faniel.dll-sample:*` settings values.

## Notes

- These samples are intentionally placeholder-grade and safe for iterative loader testing.
- The MVP visual PNG loader currently supports non-interlaced 8-bit RGB/RGBA PNGs.
- FOV slider controls now target the dedicated native-style Mods settings menu.
- Replace `persist.test` and `faniel.mvp-sample` with finalized documented sample artifacts when the content pipeline settles.
- Shareable zips for these sample folders are produced by `tools/package-release.ps1` under `packages/mods/`.

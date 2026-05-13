# Hook Map

Record both successful and failed hook attempts with enough detail for future agents.

## Status Legend

- `confirmed`: observed and suitable for current use.
- `candidate`: plausible, not yet validated in runtime tests.
- `failed`: tested and not viable for the intended behavior.

## Boot and Initialization

- `confirmed` - `BepInEx BasePlugin.Load` (`EIC.ModLoader.Plugin.Load`)
  - Source: runtime verification via `BepInEx/LogOutput.log`.
  - Purpose: current bootstrap point for mod discovery and initial loader startup logic.
  - Evidence: historical: `EIC Mod Loader bootstrap loaded.`; current branding: `Crabtya bootstrap loaded.`

- `candidate` - `Config.Initialize`
  - Source: `RuntimeInitializeOnLoads.json`
  - Purpose: early config initialization anchor.
  - Notes: Harmony probe installs successfully, but this hook has not yet been observed firing in captured startup logs.

- `candidate` - `ProgressManager.InitializeStatics`
  - Source: `RuntimeInitializeOnLoads.json`
  - Purpose: static progression initialization timing anchor.
  - Notes: Harmony probe installs successfully, but this hook has not yet been observed firing in captured startup logs.

- `confirmed` - `GameFlow.GameFlowController.InitializeRunInBackground`
  - Source: `RuntimeInitializeOnLoads.json` plus runtime verification via `BepInEx/LogOutput.log` on May 10, 2026.
  - Purpose: confirmed early game-flow lifecycle anchor after loader discovery/apply and before deeper gameplay/UI integration.
  - Evidence: `Runtime hook fired: GameFlow.GameFlowController:InitializeRunInBackground. AppliedMods=1, AppliedDefinitions=2, AppliedLocalizationEntries=2, AppliedBalancePatches=1`

## Menu/UI Timing

- `candidate` - main-menu scene post-load callback (exact method TBD)
  - Purpose: attach first runtime Mods panel without patching deep menu internals.
  - Notes: prefer scene-load/event driven hook first.

- `candidate` - runtime diagnostics component created from `GameFlow.GameFlowController.InitializeRunInBackground`
  - Source: runtime verification via `BepInEx/LogOutput.log` on May 10, 2026.
  - Purpose: confirms an injected IL2CPP `MonoBehaviour` can be created safely from the confirmed `GameFlow` hook.
  - Evidence: historical runtime-panel builds used `Runtime overlay bootstrap component started...`; current Crabtya builds use the same hook for minimal main-menu status text + native settings injectors.
  - Notes: first IMGUI/input-driven overlay attempt was not viable because this build uses the Input System package and IMGUI callbacks hit IL2CPP unstripping failures.

- `candidate` - screen-space Unity UI panel created from loader runtime component
  - Source: runtime verification via `BepInEx/LogOutput.log` on May 10, 2026.
  - Purpose: historical non-IMGUI visible surface for loader state using standard Unity UI.
  - Evidence: `Runtime overlay UI created successfully.`
  - Notes: retained as history only; v1 product direction removes loader-owned mod panel and keeps only minimal main-menu Crabtya label.

## Content Registration

- `candidate` - Addressables initialization completion
  - Purpose: gate optional catalog loads and content registration ordering.
  - Notes: must preserve safe behavior when catalogs already loaded.

- `confirmed` - `General.CameraScaler:Update`
  - Source: runtime verification via `BepInEx/LogOutput.log` on May 10, 2026.
  - Purpose: apply MVP `cameraPatch` definitions to gameplay/menu cameras.
  - Evidence: `CameraPatchApplicator: camera patch applied... Camera=UICamera... After=13.5`.

- `candidate` - `UnityEngine.Camera:FireOnPreCull`
  - Source: Unity camera API metadata inspection and loader hook candidate list.
  - Purpose: render-time camera callback to catch gameplay cameras missed by game-specific update hooks.
  - Notes: loader now supports static hooks with first `Camera` argument via signature-aware postfix binding. Runtime install/firing evidence on latest deployed DLL is still pending.

- `candidate` - `UnityEngine.Camera:FireOnPreRender`
  - Source: Unity camera API metadata inspection and loader hook candidate list.
  - Purpose: late render-time callback intended to catch camera rewrites occurring after update/late-update hooks.
  - Notes: static first-`Camera` callback remains a candidate path; gameplay firing evidence is still pending.

- `confirmed` - `UnityEngine.Camera:set_orthographicSize`
  - Source: runtime verification via `BepInEx/LogOutput.log` on May 11, 2026.
  - Purpose: intercept post-write camera size churn and keep loader patch values stable under late camera-system rewrites.
  - Evidence: startup install line `CameraPatchApplicator: hook installed: UnityEngine.Camera:set_orthographicSize (...)` on deployed DLL `Size=143360`.

- `confirmed` - `UnityEngine.Camera:set_projectionMatrix`
  - Source: runtime verification via `BepInEx/LogOutput.log` on May 11, 2026.
  - Purpose: catch projection resets that can nullify effective zoom despite orthographic-size writes.
  - Evidence: startup install line `CameraPatchApplicator: hook installed: UnityEngine.Camera:set_projectionMatrix (...)` on deployed DLL `Size=143360`.

- `confirmed` - `World.CombatCamera:LateUpdate`
  - Source: hook installed successfully on May 10, 2026.
  - Purpose: gameplay camera fallback for MVP `cameraPatch` definitions.
  - Notes: the same build also polls live cameras from the overlay component to avoid timing gaps.

- `confirmed` - `World.Characters.PlayerCharacter:Start` / `OnEnable` / `SetupInitialStats`
  - Source: hooks installed and observed during gameplay on May 10, 2026.
  - Purpose: apply MVP `visual` definitions to the live player sprite.
  - Evidence: `VisualPatchApplicator: player sprite replacement applied... RenderersChanged=363, Player=PlayerCharacter(Clone)`.

## Localization

- `candidate` - `UnityEngine.Localization.Settings.LocalizedStringDatabase:GetLocalizedString`
  - Source: runtime probe installed successfully.
  - Purpose: likely synchronous override seam for loader-owned localization entries.
  - Notes: not yet observed firing in captured runtime logs.

- `candidate` - `UnityEngine.Localization.Settings.LocalizedStringDatabase:GetLocalizedStringAsync`
  - Source: runtime probe installed successfully and observed firing in `BepInEx/LogOutput.log` on May 10, 2026.
  - Purpose: likely primary live localization consumption seam in the current game build.
  - Evidence: `Localization hook fired: UnityEngine.Localization.Settings.LocalizedStringDatabase:GetLocalizedStringAsync...`

- `candidate` - `UnityEngine.Localization.Components.LocalizeStringEvent:UpdateString`
  - Source: probe target remains plausible for UI refresh timing.
  - Purpose: potential UI-facing update seam after string lookup.
  - Notes: keep as a runtime candidate once the database-level hook path is stabilized.

## Failed Hooks

- `failed` - `World.UI.MainMenuScreen:OnEnable`
  - Source: type-name candidate inferred from interop strings.
  - Outcome: Harmony probe could not resolve the type at runtime on May 10, 2026.
  - Notes: likely wrong type name or wrong lifecycle method; use scene-driven discovery or more targeted symbol extraction before retrying.

- `failed` - `UnityEngine.Localization.Operations.GetLocalizedStringOperation:Execute`
  - Source: runtime probe attempt recorded in `BepInEx/LogOutput.log` on May 10, 2026.
  - Outcome: Harmony IL2CPP patching failed during plugin load with `Cannot get result from void method`.
  - Notes: do not retry this probe shape as a generic result-style postfix; prefer database-level hooks instead.

## Update Rules

- Add each attempted hook with date, context, and outcome.
- Keep failed hooks for historical trace; do not delete them.

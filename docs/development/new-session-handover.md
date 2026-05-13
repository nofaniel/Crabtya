# New Session Handover

## Start Here

- Read `docs/README.md` first for current Crabtya scope.
- Use `docs/development/agent-handoff.md` for latest implementation state and next actions.
- Use `docs/development/test-checklist.md` for verified vs pending runtime proof.

## Current Milestone

- Active milestone: Crabtya v1 implementation hardening.
- Status: implementation is now aligned to PLAN.md direction, but fresh runtime evidence is still required after this refactor pass.
- Core direction now in code:
  - folder mods under `game/Mods/<mod-id>/`;
  - `eicmod.json` manifest contract;
  - declarative + DLL mod support;
  - no loader-owned runtime mod panel;
  - minimal main-menu Crabtya status text;
  - native-styled main-menu `MODS` button plus dedicated native-style Mods settings menu.

## Latest Build Snapshot

- Build succeeds:
  - `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`
- DLL sample build succeeds:
  - `dotnet build loader/SampleDllMod/SampleDllMod.csproj -c Release`
- Packaging script succeeds:
  - `tools/package-release.ps1 -Clean -ReleaseLabel crabtya-kickoff`
  - loader zip includes `BepInEx/plugins/Crabtya.ModApi.dll` and `BepInEx/plugins/EIC.ModLoader.dll`.

## Runtime Contract to Preserve

- `Enabled` remains persisted startup-applied state.
- `EffectiveEnabled` remains current-launch activation state.
- Startup queue remains `Mods/startup-commands.json` consumed on startup.
- Safe mode remains `--eic-safe-mode` / `eic-safe-mode.flag`.
- `uiScale` is supported and scales native UI elements individually safely.

## Definition Scope (Current)

Applied:

- `localization`
- `balancePatch`
- `cameraPatch`
- `visual`
- `introSkip`

Recognized but disabled/future:

- `uiScale`
- `evolution` (future/discovery-only)
- `enemy` (future/discovery-only)

## DLL and Settings Scope (Current)

- DLL contract in manifest uses paired `assemblies` + `entrypoints`.
- Public API is `Crabtya.ModApi.dll` with:
  - `ICrabtyaMod`
  - `ICrabtyaModContext`
  - `ICrabtyaLogger`
  - `ICrabtyaSettingsRegistry`
- Settings registry supports:
  - `bool`
  - `int`
  - `float`
  - option/string list

## Immediate Next Actions

1. Launch game and capture fresh `LogOutput.log` proof for Crabtya branding and no-panel behavior.
2. Verify main-menu-only loader-owned UI signal (`Crabtya loaded v<version>`).
3. Verify dedicated Mods menu rows render and persist for `faniel.fov-slider` and `faniel.dll-sample` settings.
4. Verify DLL sample entrypoint load logs and isolation behavior with one intentionally broken DLL mod.
5. Refresh docs/checklists with new runtime evidence.

## Constraints

- Do not modify original game binaries/assets under `game/Everything is Crab_Data/`.
- Keep hook-failure history intact in `docs/development/hook-map.md`.
- Keep docs synchronized with behavior changes in same task.

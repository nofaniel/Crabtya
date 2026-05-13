# Job 07 - Fix UI Scale Popping and Polish UI Scale Mod

Source TODO paragraph: "Fix UI popping issue with UI scaling mod, improve mod overall"

## Objective

Finish polishing the `faniel.ui-scale` experience by eliminating visible UI popping where possible, preserving native layouts across scenes/menus, and improving the mod's user-facing behavior and documentation.

## Status Update (2026-05-13)

- Complete.
- Code-side polish landed:
  - adaptive runtime tick cadence for `UiScaleApplicator` (`~0.05s` while scale is non-`1x`, `~0.25s` while at `1x`), wired from `RuntimeOverlay`;
  - transition-aware burst cadence now briefly runs at near-frame cadence (`~0.016s` for `~0.8s`) when eligible UI root selection changes, reducing residual popping during pause open and return-to-main-menu transitions;
  - transition-sensitive quit confirmation popup handling now keeps near-frame cadence while `GenericDecisionPopup` is active to reduce first-frame pop;
  - Crabtya cloned menu buttons now copy baseline/native template scale (captured via `UiScaleApplicator`) instead of potentially already-scaled transform values, preventing oversized MODS clones at non-`1x`;
  - stale/dead helper paths in `UiScaleApplicator` were removed to keep root eligibility logic focused on the live strategy.
- Docs polish landed for `faniel.ui-scale` usage guidance and mod-maker behavior notes.
- Runtime QA/evidence capture is complete based on in-game validation feedback (`0.75x`/`1.15x` emphasis plus return-to-`1x` behavior), including pause/open, quit-to-main, quit-confirm popup, and main-menu `MODS` button size parity checks.

## Scope

Work in `loader/EIC.ModLoader/UiScaleApplicator.cs`, `RuntimeOverlay.cs`, `CrabtyaModsSettingsWindow.cs` only if menu setting behavior needs adjustment, and `game/Mods/faniel.ui-scale/` docs/definition. Use the existing screenshot evidence in repo root as context, but verify against current runtime behavior rather than assuming older bugs still exist.

## Implementation Plan

1. Reproduce current UI scale behavior from a clean baseline: main menu, Mods window, level select, gameplay HUD, pause menu, reward/progression surfaces, and return to `1x`.
2. Capture before/after screenshots only when useful; keep final evidence names clear and avoid accumulating ambiguous one-off files.
3. Review current baseline-capture logic, eligible target selection, stability gate, per-tick interval, presentation UI caps, and excluded Crabtya UI paths.
4. Identify the remaining source of popping: late-discovered RectTransforms, layout animations, baseline capture during transition, repeated restore/apply cycles, scene-specific object creation, or over-broad target eligibility.
5. Prefer targeted fixes that preserve game-owned layout and animation: defer capture until stable, pre-apply scale earlier when safe, reduce scan interval only where necessary, and avoid mutating anchors/pivots/positions unless absolutely required.
6. Improve the `faniel.ui-scale` mod metadata/README to explain safe ranges, known limitations, and how to reset to `1x`.
7. Update docs/checklist with exact runtime proof for scale down, scale up, and return-to-native behavior.

## Deliverables

- Reduced or eliminated visible UI scale popping in the affected menus.
- Safer target eligibility or baseline handling if bugs are found.
- Updated `faniel.ui-scale` README and any relevant mod-maker docs.
- Fresh runtime evidence recorded in `docs/development/agent-handoff.md` and `docs/development/test-checklist.md`.

## Verification

- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- Runtime: test `0.5x`, `0.75x`, `1x`, and `1.15x` where applicable.
- Runtime: main menu, level select, gameplay HUD, pause menu, and Mods settings window remain aligned with no cumulative drift.
- Runtime: open/close menus repeatedly at non-`1x` scale and confirm no delayed snap or layout corruption beyond known game-owned animations.
- Runtime: `ErrorLog.log` remains empty.

## Completion Criteria

- UI scale feels stable enough for a public sample mod.
- Any remaining unavoidable popping is documented honestly with reproduction notes.
- The mod improves accessibility without corrupting native UI layouts.

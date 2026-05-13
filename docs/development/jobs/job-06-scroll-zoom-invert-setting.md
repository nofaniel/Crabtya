# Job 06 - Add Scroll Zoom Invert Setting

Source TODO paragraph: "Scroll Zoom Mod: Add setting for scroll invert to change zoom direction from scrolling"

## Objective

Add a user-facing setting for the Mouse Wheel Zoom mod so players can invert scroll direction without editing files manually. The setting should persist in `game/Mods/runtime-settings.json` and apply live through the existing Crabtya Mods settings menu.

## Scope

Work in the generic loader only as needed to support the setting cleanly, plus `game/Mods/faniel.mousewheel-zoom/`, `templates/` if the pattern belongs in templates, and mod-maker docs if the manifest schema changes. The feature should remain data-driven and not hard-code `faniel.mousewheel-zoom` behavior into generic loader code beyond interpreting a generic cameraPatch mouse-wheel setting.

## Implementation Plan

1. Inspect `MouseWheelZoomApplicator`, `ContentDefinitions`, `ContentPipeline`, `RuntimeModSettings`, and `CrabtyaModsSettingsWindow` to understand how cameraPatch `settings.mouseWheel` is modeled and persisted.
2. Extend the camera patch mouse-wheel settings schema with an invert flag or registered setting. Prefer a generic field such as `settings.invertSetting` or `settings.invertScroll` that can be reused by any wheel-driven camera patch.
3. Add a Mods menu row for the invert setting if the current settings renderer does not automatically expose it.
4. Update `MouseWheelZoomApplicator` so wheel delta direction is inverted when the persisted setting is true.
5. Update `game/Mods/faniel.mousewheel-zoom/defs/mousewheel-zoom.json` and README to document the setting.
6. Ensure existing users with no persisted invert value default to the current behavior.
7. Update `docs/mod-makers/manifest-format.md` with the new field and example.

## Deliverables

- Invert scroll setting visible in the dedicated Crabtya Mods settings menu.
- Runtime persistence through `Mods/runtime-settings.json`.
- Updated mouse-wheel sample mod definition and README.
- Updated mod-maker docs if the manifest schema changes.

## Status (2026-05-13) — Done, runtime verified

`Invert Scroll` toggle confirmed present in the Mods settings window for `faniel.mousewheel-zoom`. Toggling it flips zoom direction live. `runtime-settings.json` updated with `faniel.mousewheel-zoom.gameplay-fov.invert` key. `ErrorLog.log` empty.

## Verification

- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- Runtime: enable `faniel.mousewheel-zoom`, open Mods settings, toggle invert, enter gameplay, and confirm scroll up/down direction changes.
- Runtime: setting persists after restart in `game/Mods/runtime-settings.json`.
- Runtime: with invert unset or false, current scroll behavior is unchanged.
- `ErrorLog.log` remains empty.

## Completion Criteria

- Players can invert scroll zoom direction from the Mods settings menu.
- The setting is generic enough for future mouse-wheel camera patches.
- The mod remains content-driven and cleanly separated from loader internals.

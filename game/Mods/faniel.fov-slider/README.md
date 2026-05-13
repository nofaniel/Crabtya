# Gameplay FOV Slider (`faniel.fov-slider`)

## What this mod does

- Adds a native settings row `FOV` slider for gameplay camera zoom.
- Uses one `cameraPatch` definition that multiplies `camera.orthographicSize`.
- Persists your chosen value to `game/Mods/runtime-settings.json`.

## Construction

Folder layout:

- `eicmod.json`: manifest and mod metadata.
- `defs/fov-slider.json`: the single `cameraPatch` definition.

Definition details (`defs/fov-slider.json`):

- `type`: `cameraPatch`
- `id`: `faniel.fov-slider.gameplay-fov`
- `target`: `camera.orthographicSize`
- `op`: `multiply`
- `value`: `1.0` (default baseline multiplier)
- `settings`:
  - `slider`: `true` (enables live panel slider)
  - `label`: `FOV`
  - `min`: `0.75`
  - `max`: `2.0`
  - `step`: `0.05`

## User usage

1. Enable `faniel.fov-slider` (startup toggle or `mod-state.json` persisted enable).
2. Keep `faniel.mousewheel-zoom` disabled if you want slider-only control.
3. Open game settings and move the injected Crabtya `FOV` slider row.
4. Changes apply immediately in gameplay and are saved for next launch.

## Runtime behavior notes

- Value is read from `runtime-settings.json` by patch ID.
- Camera changes are applied live and re-applied by camera hooks/runtime loop.
- Range is clamped to `0.75` to `2.0`.

## Troubleshooting

- If no visible change occurs, confirm mod is `Active now: Yes` in the panel.
- Check `game/BepInEx/LogOutput.log` for `CameraPatchApplicator` apply lines.
- Confirm `game/BepInEx/ErrorLog.log` is empty.

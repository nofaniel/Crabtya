# Mouse Wheel Zoom (`faniel.mousewheel-zoom`)

## What this mod does

- Adds live mouse-wheel zoom for gameplay camera scaling.
- Uses one `cameraPatch` definition that multiplies `camera.orthographicSize`.
- Persists wheel-selected zoom values to `game/Mods/runtime-settings.json`.

## Construction

Folder layout:

- `eicmod.json`: manifest and mod metadata.
- `defs/mousewheel-zoom.json`: the single `cameraPatch` definition.

Definition details (`defs/mousewheel-zoom.json`):

- `type`: `cameraPatch`
- `id`: `faniel.mousewheel-zoom.gameplay-fov`
- `target`: `camera.orthographicSize`
- `op`: `multiply`
- `value`: `1.0` (default baseline multiplier)
- `settings`:
  - `mouseWheel`: `true` (enables wheel-driven live control)
  - `invert`: `false` (default scroll direction; user can flip from the Mods settings menu)
  - `label`: `Wheel Zoom`
  - `min`: `0.75`
  - `max`: `3.0`
  - `step`: `0.05`

## User usage

1. Enable `faniel.mousewheel-zoom`.
2. Disable `faniel.fov-slider` if you want wheel-only control and no slider interaction.
3. Enter gameplay and use mouse wheel up/down to adjust zoom.
4. Value applies live and is saved automatically.
5. To flip scroll direction, open the Mods settings menu and toggle `Invert Scroll` under this mod. The choice persists in `game/Mods/runtime-settings.json`.

## Runtime behavior notes

- Wheel input is read each frame by Crabtya runtime behavior.
- Each wheel direction step changes zoom by `step` (`0.05` by default).
- Value is clamped to `0.75` to `3.0` and saved by patch ID.

## Troubleshooting

- Ensure `faniel.mousewheel-zoom` is enabled in `mod-state.json` and active this launch.
- Check `game/BepInEx/LogOutput.log` for `Runtime overlay mouse-wheel zoom: set 'faniel.mousewheel-zoom.gameplay-fov' ...` entries.
- Confirm `game/Mods/runtime-settings.json` includes `faniel.mousewheel-zoom.gameplay-fov`.

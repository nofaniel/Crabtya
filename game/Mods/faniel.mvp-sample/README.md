# MVP Sample Mod (`faniel.mvp-sample`)

## What this mod does

- Widens gameplay camera framing through one `cameraPatch`.
- Replaces the player base sprite from a mod-local PNG through one `visual` definition.

## Construction

Folder layout:

- `eicmod.json`: manifest and mod metadata.
- `defs/camera.json`: gameplay camera multiplier.
- `defs/player-sprite.json`: local sprite replacement.
- `assets/base-crab.png`: replacement sprite asset.

## User usage

1. Extract the mod zip into `game/Mods/`.
2. Launch the game and enter gameplay.
3. Confirm the camera is wider and the player sprite uses the packaged PNG.

## Troubleshooting

- Check `game/BepInEx/LogOutput.log` for `AppliedCameraPatches=1` and `AppliedVisualPatches=1`.
- Disable and restart to confirm vanilla camera/sprite behavior returns.

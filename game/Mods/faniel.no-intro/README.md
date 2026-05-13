# No Intro (`faniel.no-intro`)

## What this mod does

- Skips intro and splash video playback before the main menu.
- Uses one `introSkip` definition with substring matching against runtime video probes.

## Construction

Folder layout:

- `eicmod.json`: manifest and mod metadata.
- `defs/no-intro.json`: the `introSkip` definition.

## User usage

1. Extract the mod zip into `game/Mods/`.
2. Launch the game normally.
3. Confirm the intro/splash sequence fast-forwards instead of playing through.

## Troubleshooting

- Check `game/BepInEx/LogOutput.log` for `IntroSkipApplicator` hook/apply lines.
- Confirm `game/Mods/mod-state.json` shows `EffectiveEnabled=true` for `faniel.no-intro`.

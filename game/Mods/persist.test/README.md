# Sample Content Slice (`persist.test`)

## What this mod does

- Adds one `balancePatch` that increases `player.stat.NumberOfStandardEvolutionChoices` by `2`.
- Adds one `localization` definition with visible UI/evolution-card text overrides.

## Construction

Folder layout:

- `eicmod.json`: manifest and mod metadata.
- `defs/d1.json`: the `balancePatch` definition.
- `defs/loc.en.json`: the localization override set.

## User usage

1. Extract the mod zip into `game/Mods/`.
2. Launch the game and start a run.
3. Reach an evolution choice and confirm the extra standard choices are visible.
4. Disable the mod and restart to confirm vanilla behavior returns.

## Troubleshooting

- Check `game/BepInEx/LogOutput.log` for balance hook install/apply lines.
- Check `game/Mods/mod-state.json` for `AppliedBalancePatchCount=1` and `AppliedLocalizationEntryCount=7`.

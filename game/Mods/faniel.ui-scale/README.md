# UI Scale (`faniel.ui-scale`)

## Current status

- This mod is fully functional.
- The loader discovers `uiScale` definitions and applies a uniform scale to native UI controls from their captured baseline, preserving the game's original anchors, pivots, and layout containers.
- Large presentation UI, such as level-select reward/progression elements, can shrink below native size but is capped at native size when the slider is above `1x`.
- Enabling the mod allows you to scale UI elements through the Crabtya Mods settings menu.

## Recommended usage

- Default (`1.0x`) is the native game size.
- Comfortable compact range is usually `0.75x` to `1.0x`.
- `0.5x` is available for maximum space savings but may feel very small on lower resolutions.
- `1.15x` is the intended upper bound for accessibility without overgrowing large presentation surfaces.

## Runtime behavior notes

- UI scale is applied live and does not require restart.
- The applicator uses a faster refresh while scale is not `1x` and a slower idle refresh at `1x`, reducing visible pop while keeping baseline CPU overhead lower.
- When major menu roots change (for example pause open/close or return to main menu), Crabtya briefly runs near-frame refresh to smooth transition popping.
- Quit confirmation popup transitions are also treated as transition-sensitive and use near-frame refresh while active.
- Crabtya's dedicated Mods settings window is excluded from the `uiScale` pass.
- Crabtya's injected `MODS`/`MOD SETTINGS` button clones inherit native baseline scale so they match nearby menu buttons under non-`1x` scaling.

## Reset to native UI

- Open the Crabtya Mods window and set `UI Scale` back to `1.0x`.
- This returns eligible UI elements to captured native baselines.
- If you want to fully clear the persisted value, remove `faniel.ui-scale.global` from `game/Mods/runtime-settings.json` while the game is closed.

## Construction

Folder layout:

- `eicmod.json`: manifest and mod metadata.
- `defs/ui-scale.json`: the `uiScale` definition.

## Why it is shareable

- It documents the current manifest shape for `uiScale`.
- It demonstrates declarative UI mutation hooks via `eicmod.json`.

## Troubleshooting

- Scale changes should reflect instantly.
- If UI looks unstable during heavy scene/menu transitions, wait for the transition to finish and re-check at the same scale.
- If UI elements appear off-screen, return to `1.0x` first, then test whether another mod is also mutating UI transforms.

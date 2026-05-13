# Mods Menu Integration Notes

## Current Decision

Crabtya v1 does not ship the old runtime overlay Mods panel.

Crabtya v1 does ship:

- main-menu Crabtya status label;
- native-styled `MODS` button injection;
- dedicated native-style Mods settings menu window.

This dedicated menu is the authoritative settings/toggle UX for v1.

## Runtime Path

- Main-menu button injection:
  - `loader/EIC.ModLoader/MainMenuModsButtonApplicator.cs`
- Dedicated Mods settings menu window:
  - `loader/EIC.ModLoader/CrabtyaModsSettingsWindow.cs`
- Legacy native settings injector (intentionally disabled):
  - `loader/EIC.ModLoader/CrabtyaNativeSettingsApplicator.cs`
- Mod settings registry:
  - `loader/EIC.ModLoader/CrabtyaSettingsRegistry.cs`

## Supported Setting Kinds

- `bool`
- `int`
- `float`
- option/string list

Persistence remains loader-owned in `game/Mods/runtime-settings.json`.

## Failure Behavior

- If menu template cloning fails, Crabtya should fall back to safe local controls and continue.
- One setting render failure must not block mod loading or crash the game.
- If dedicated menu rendering fails, log clearly and preserve loader stability.

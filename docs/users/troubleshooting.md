# Troubleshooting

## Game Fails to Launch After Loader Install

- Verify files were extracted beside `Everything is Crab.exe`.
- Launch in safe mode to confirm base game boot.
- Check latest loader logs for missing dependencies or initialization errors.

## Game Launches But Mods Do Not Apply

- Verify mod folder path is `Mods/<mod-id>/`.
- Confirm `eicmod.json` exists and required fields are present.
- Check dependency IDs/versions and duplicate mod IDs.
- Check if restart is required for recent catalog/code changes.
- Check `Mods/mod-state.json` for `RestartRequired` and `RestartReasons` after using startup toggle arguments.
- Check `game/BepInEx/LogOutput.log` for the `Plugin binary fingerprint` line to confirm the game loaded the expected `EIC.ModLoader.dll` build.
- If you used startup queue tooling, confirm `Mods/startup-commands.json` and `PendingStartupQueued*` reflect the intended next-launch action.
- Confirm the mod is actually active this session (`EffectiveEnabled=true`), not only queued for next launch.

## Mod Causes Errors

- Disable the mod via launch arg (`--eic-disable-mod=<mod-id>`) or `Mods/mod-state.json`.
- Retry launch in safe mode if normal launch fails.
- Review logs for manifest validation or content load errors.

## Quick Toggle From Launch Options

- Temporarily force-disable a mod for the next launch with:
  - `--eic-disable-mod=<mod-id>`
- Re-enable with:
  - `--eic-enable-mod=<mod-id>`
- Combine with safe mode when recovering from startup crashes.

## Unexpected UI Behavior

- If the old in-run overlay/panel appears, that is a regression for Crabtya v1.
- Expected Crabtya UI on main menu is:
  - small loaded/version label;
  - native-styled `Mods` button.
- Mod settings should appear through the `Mods` entry in the dedicated native-style Mods settings menu.

## Save Isolation And Achievements

- Crabtya v1 runs with base-game save isolation and Steam achievement blocking enabled.
- Modded-session save files are redirected to `game/CrabtyaData/IsolatedSave/`.
- If you expected progress in the base save profile or Steam achievements, launch without the loader.
- If sandbox saves are not appearing, check `game/BepInEx/LogOutput.log` for `SessionIsolationGuard` startup lines.

## Version Mismatch Warnings

- If `targetGame` or `targetUnity` differs, treat mod as potentially unsafe.
- Prefer disabling until mod is updated.

## Reporting Issues

- Include loader version, game version, mod ID/version, and relevant log excerpts.
- Include steps to reproduce from clean launch to failure.
- Include `game/Mods/mod-state.json` and (if present) `game/Mods/startup-commands.json`.

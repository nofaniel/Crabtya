# Crabtya Lite (QoL-Only)

Crabtya Lite is a separate plugin for players who only want built-in quality-of-life settings.

It is **not** a folder-based mod loader. It does not scan `game/Mods/`, does not load user mod manifests, and does not block achievements or use an isolated save.

## Crabtya Lite vs Full Crabtya

| Feature | Crabtya Lite | Full Crabtya |
|---|---|---|
| Mouse-wheel zoom | Yes | Via `faniel.mousewheel-zoom` mod |
| Invert Scroll | Yes | Via `faniel.mousewheel-zoom` mod |
| FOV slider | Yes | Via `faniel.ui-scale` mod |
| User folder mods (`game/Mods/`) | **No** | Yes |
| `eicmod.json` manifests | **No** | Yes |
| DLL mod entrypoints | **No** | Yes |
| Mods settings menu | **No** | Yes |
| Save isolation | **No** | Yes |
| Achievement blocking | **No** | Yes |
| Settings persistence | `game/CrabtyaLite/settings.json` | `game/Mods/runtime-settings.json` |

**Choose Crabtya Lite** if you only want QoL camera controls and do not intend to install community mods.

**Choose full Crabtya** if you want folder-based user mods, content mods, DLL mods, mod settings, or the safer isolated-save modded play session.

**Do not install both** at the same time. Lite will detect the full loader and refuse to run to prevent mixed-install conflicts.

## What Lite Includes

- Mouse wheel zoom.
- Invert Scroll toggle.
- FOV slider in native-feeling settings UI.
- Lite-only settings persistence at `game/CrabtyaLite/settings.json`.

## What Lite Does Not Include

- No `game/Mods/` discovery.
- No `eicmod.json` manifests.
- No DLL mod entrypoint loading.
- No Crabtya Mods menu or mod enable/disable UI.
- No startup mod queue files (`mod-state.json` / `startup-commands.json`) owned by Lite.

## Install Crabtya Lite

1. Close the game.
2. Extract `Crabtya-Lite-<version>.zip` to a temporary folder.
3. Open PowerShell in that folder and run:

```powershell
.\Install-CrabtyaLite.ps1 -GamePath "C:\path\to\Everything is Crab"
```

4. Launch the game and check `game/BepInEx/LogOutput.log` for:
   - `Loading [Crabtya Lite ...]`
   - `Crabtya Lite runtime initialized.`

## Coexistence Rule

- By default, Lite blocks mixed installs when full Crabtya is present.
- For local testing only, installer override is available:

```powershell
.\Install-CrabtyaLite.ps1 -GamePath "C:\path\to\Everything is Crab" -AllowFullCoexist
```

Do not use mixed installs for normal gameplay.

## Uninstall Crabtya Lite

```powershell
.\Uninstall-CrabtyaLite.ps1 -GamePath "C:\path\to\Everything is Crab"
```

This removes:

- `BepInEx/plugins/Crabtya.Lite.dll`
- `CrabtyaLite/` settings folder
- copied Lite install/uninstall scripts

It intentionally does **not** remove full Crabtya if present.

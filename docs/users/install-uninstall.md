# Install and Uninstall

## Install (v1)

### Option A — PowerShell installer script (recommended)

1. Close the game.
2. Extract the loader package zip to a temporary folder.
3. Open PowerShell in that folder and run:
   ```powershell
   .\Install-Crabtya.ps1 -GamePath "C:\path\to\Everything is Crab"
   ```
   The script validates the game path, copies all loader files, and prints a checklist.
4. Launch the game — the log and main menu confirm a successful install.

### Option B — Manual extract

1. Close the game.
2. Extract the loader package contents into the game folder beside `Everything is Crab.exe`.
3. Extract each mod package into `Mods/` so the manifest lands at `Mods/<mod-id>/eicmod.json`.
4. Launch the game normally.

The loader will create `Mods/`, `Mods/_disabled/`, and `Mods/mod-state.json` automatically on first run.

## Release Package Layout

Loader zip contents are expected at the zip root:

- `BepInEx/`
- `dotnet/`
- `docs/`
- `README.txt`
- `doorstop_config.ini`
- `winhttp.dll`
- `.doorstop_version`
- `changelog.txt`

Each mod zip is expected to contain one root folder:

- `<mod-id>/eicmod.json`
- `<mod-id>/README.md` (recommended and now shipped with current sample mods)
- `<mod-id>/...content files...`

If extraction creates an extra nested folder above `BepInEx/` or above `<mod-id>/`, move the inner contents up one level before launching.

## Loader Files Added to the Game Folder

- `BepInEx/`
- `dotnet/`
- `doorstop_config.ini`
- `winhttp.dll`
- `.doorstop_version`
- `changelog.txt`

## First-Run Validation

- `game/BepInEx/LogOutput.log` is created.
- Startup includes `Plugin binary fingerprint` for `EIC.ModLoader.dll`.
- `game/Mods/mod-state.json` is created and includes discovered mods.
- Main menu shows a small `Crabtya loaded v<version>` label.
- Main menu shows a `Mods` entry matching native button styling.
- No old loader runtime overlay panel should appear.

## Install From Repo-Built Packages

If you build packages locally with `tools/package-release.ps1`, the artifacts land at:

- `packages/loader/EverythingIsCrab.ModLoader-<release-label>.zip`
- `packages/mods/<mod-id>-<version>.zip`
- `packages/package-summary.json`

## Queueing Mod Changes (Restart-First)

- Startup queue semantics remain available for developer/admin workflows.
- Queue state is stored in:
  - `game/Mods/startup-commands.json`
  - `game/Mods/mod-state.json` pending fields
- Queue commands are consumed on next startup.

## Disable a Mod

- Preferred: use launch arg `--eic-disable-mod=<mod-id>`, then restart.
- Manual fallback: set that mod's `Enabled` to `false` in `game/Mods/mod-state.json` while game is closed.
- Optional organization: move the mod folder into `game/Mods/_disabled/`.

## Uninstall Loader

### Option A — PowerShell uninstaller script (recommended)

```powershell
.\Uninstall-Crabtya.ps1 -GamePath "C:\path\to\Everything is Crab"
# To also remove mod data:
.\Uninstall-Crabtya.ps1 -GamePath "C:\path\to\Everything is Crab" -RemoveMods
```

### Option B — Manual removal

1. Close the game.
2. Remove loader-added files/folders only:
   - `BepInEx/`
   - `dotnet/`
   - `doorstop_config.ini`
   - `winhttp.dll`
   - `.doorstop_version`
   - `changelog.txt`
3. Keep or remove `Mods/` depending on whether you want to preserve mod data.
4. Launch the game to confirm vanilla startup.

## Safety Notes

- Do not replace or edit original game files during normal mod install/uninstall.
- If target/version mismatch warnings appear, treat the mod as unsafe until updated.

## Crabtya Lite (QoL-only)

Crabtya Lite is a separate post-v1 package for built-in QoL settings only (zoom/invert/FOV), not folder mods.

- Lite guide: `docs/users/crabtya-lite.md`
- Lite install script: `Install-CrabtyaLite.ps1`
- Lite uninstall script: `Uninstall-CrabtyaLite.ps1`

Do not install full Crabtya and Crabtya Lite together unless you are intentionally testing coexistence overrides.

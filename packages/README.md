# Crabtya v1.0.0 — Release Artifacts

Generated artifacts live here after running `tools/package-release.ps1`.

## How to Regenerate

```powershell
# v1.0.0 release build (release-eligible mods only)
.\tools\package-release.ps1 -ReleaseLabel "v1.0.0" -Clean `
    -ModId faniel.dll-sample,faniel.fov-slider,faniel.mousewheel-zoom,faniel.mvp-sample,faniel.no-intro,faniel.ui-scale
```

## Artifacts

### `loader/EverythingIsCrab.ModLoader-v1.0.0.zip`

The full Crabtya loader package. Extract into the folder beside `Everything is Crab.exe`.

**Contents:**
- `BepInEx/` — BepInEx IL2CPP runtime (core, interop, unity-libs, config, plugins)
- `BepInEx/plugins/EIC.ModLoader.dll` — Crabtya loader plugin
- `BepInEx/plugins/Crabtya.ModApi.dll` — Public mod API (referenced by DLL mods)
- `dotnet/` — .NET runtime required by BepInEx
- `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` — BepInEx doorstop
- `Install-Crabtya.ps1` / `Uninstall-Crabtya.ps1` — PowerShell helpers
- `docs/` — User docs (install-uninstall, safe-mode, troubleshooting)
- `templates/` — Mod starter kits (content-mod-template, dll-mod-template)
- `README.txt` — Quick-install instructions with plugin fingerprint

**Install:**
```powershell
.\Install-Crabtya.ps1 -GamePath "C:\path\to\Everything is Crab"
```

**Fingerprint verification (after install):**
Check `game/BepInEx/LogOutput.log` for a line containing `Plugin binary fingerprint`. The size should match `package-summary.json → loaderPackage.PluginSize`.

### `templates/content-mod-template-v1.0.0.zip`

Standalone starter kit for content-only mods (declarative `balancePatch`, `cameraPatch`, `localization`, `visual`, `uiScale`, `introSkip`). No C# required.

Extract and rename `content-mod-template/` to your mod ID. Edit `eicmod.json` and the `defs/` files.

### `templates/dll-mod-template-v1.0.0.zip`

Standalone starter kit for DLL mods using `Crabtya.ModApi`. Includes `eicmod.json`, `src/ModEntrypoint.cs`, and a `.csproj` wired to the API.

### `mods/`

Individual mod packages. Each zip is rooted at `<mod-id>/` so users extract directly into `game/Mods/`.

| File | Name | Notes |
|---|---|---|
| `faniel.dll-sample-1.0.0.zip` | Sample DLL Mod | Reference DLL entrypoint mod using `Crabtya.ModApi` |
| `faniel.fov-slider-1.0.0.zip` | Gameplay FOV Slider | Slider-driven FOV cameraPatch; requires restart |
| `faniel.mousewheel-zoom-1.0.0.zip` | Mouse Wheel Zoom | Scroll-to-zoom with optional Invert Scroll toggle |
| `faniel.mvp-sample-1.0.0.zip` | MVP Sample Mod | Minimal content-only reference mod |
| `faniel.no-intro-1.0.0.zip` | No Intro | `introSkip` declarative mod; skips the opening cinematic |
| `faniel.ui-scale-1.0.0.zip` | UI Scale Settings | Slider-driven UI scale (0.5×–1.15×); live without restart |

**Install a mod:**
```
Extract faniel.mousewheel-zoom-1.0.0.zip into game/Mods/
→ game/Mods/faniel.mousewheel-zoom/eicmod.json  ✓
```

## `package-summary.json`

Machine-readable summary of the most recent packaging run: timestamp, release label, plugin size/timestamp (fingerprint data), per-mod validation warnings, and template names. Regenerated each time `package-release.ps1` runs.

## Notes

- `packages/_staging/` is temporary and deleted after a successful run.
- The loader package does **not** include `Everything is Crab.exe`, `GameAssembly.dll`, or anything under `Everything is Crab_Data/`.
- Test fixtures (`test.*`, `crabtya.bundle-test`, `persist.test`) are intentionally excluded from release packages.
- To add or remove mods from the release list, change the `-ModId` array in the regeneration command above.

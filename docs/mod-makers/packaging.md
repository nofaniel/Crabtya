# Packaging

## Packaging Goals

- Produce a shareable zip with deterministic layout.
- Catch manifest/path/dependency mistakes before release.
- Ship one loader package plus one zip per mod so releases are easy to hand to users.

## Expected Package Layout

Loader package output:

```text
packages/loader/EverythingIsCrab.ModLoader-<release-label>.zip
  BepInEx/
  dotnet/
  docs/
  README.txt
  .doorstop_version
  changelog.txt
  doorstop_config.ini
  winhttp.dll
```

Mod package output:

```text
packages/mods/<mod-id>-<version>.zip
  <mod-id>/
    eicmod.json
    README.md
    ...content files...
```

## Pre-Package Validation Checklist

- Manifest JSON parses and required fields exist.
- `id`, `version`, `targetGame`, and `loaderVersion` are set correctly.
- Referenced asset bundle/catalog/definition paths exist.
- DLL mods declare both `assemblies` and `entrypoints` together.
- Dependency IDs resolve to known mod IDs.
- Manual review confirms you are not redistributing proprietary game assets.

## Packaging Tool

- Script: `tools/package-release.ps1`
- Default output root: `packages/`
- Default behavior:
  - packages the current loader install from `game/`
  - packages every mod folder under `game/Mods/` that contains `eicmod.json`
  - writes `packages/package-summary.json`
- Optional filters:
  - `-ModId <id>` packages only selected mod IDs
  - `-SkipLoader` packages mods only
  - `-SkipMods` packages loader only
  - `-ReleaseLabel <label>` changes loader zip naming
  - `-Clean` removes previous `packages/loader`, `packages/mods`, and staging output before rebuilding

## Validation Rules Used By The Script

- Matches current loader discovery behavior for:
  - required manifest fields enforced today (`id`, `name`, `version`, `author`, `description`, `targetGame`, `loaderVersion`, `content`)
  - reverse-domain `id` validation
  - target game hard-fail and target Unity warning behavior
  - relative-path checks for `assemblies`, `content.catalogs`, `content.assetBundles`, and `content.definitions`
  - DLL contract pairing checks for `assemblies` + `entrypoints`
  - missing dependency detection
- Emits warnings for recognized future definition types (`evolution`, `enemy`) and unsupported definition types, matching loader discovery semantics.
- Packages the full mod folder contents, but roots the zip at manifest `id` so extraction lands at `Mods/<mod-id>/...`.
- Does not attempt to prove asset provenance. Treat no-redistribution review as a manual release gate.

## Example Usage

Package the current loader and every sample mod:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -Clean -ReleaseLabel v1-candidate
```

Package only the loader and `faniel.no-intro`:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1 -Clean -ModId faniel.no-intro
```

## Install Test Before Release

1. Install package into a clean `Mods/` setup.
2. Launch game and verify mod appears and enables.
3. Verify disable/re-enable behavior and restart-required messaging where applicable.
4. For the loader package, verify startup includes the `Plugin binary fingerprint` line for the expected DLL.
5. For DLL mods, verify `Crabtya.ModApi.dll` is included in the loader package under `BepInEx/plugins/`.

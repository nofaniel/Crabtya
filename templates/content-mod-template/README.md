# Content Mod Template

A starter template for a **declarative content-only** mod for *Everything is Crab* using Crabtya.

## What this template includes

| File | Purpose |
|---|---|
| `eicmod.json` | Mod manifest — fill in your id, name, author, description |
| `defs/camera.json` | Camera zoom patch with a settings slider |
| `defs/localization.en.json` | English text replacements |
| `defs/balance.json` | Balance/stat patch |
| `defs/visual.json` | Player sprite replacement |
| `defs/intro-skip.json` | Skip intro/splash videos |
| `assets/` | Place your PNG images here |

## Quick start

1. Copy this folder into `game/Mods/` and rename it to your mod ID (e.g. `yourname.my-mod`).
2. Open `eicmod.json` and update the required fields:
   - `id` — unique reverse-domain ID (e.g. `yourname.my-mod`)
   - `name`, `version`, `author`, `description`
3. Edit or remove definition files you don't need.
4. If you use the `visual` definition, place your PNG in `assets/` and update the `image` path.
5. Remove any definitions you aren't using from `eicmod.json`'s `content.definitions` list.
6. Launch the game — Crabtya will load your mod automatically.

## Mod ID format

Mod IDs must be reverse-domain style: `author.mod-name`.  
Examples: `alice.camera-wide`, `bob.no-intro`, `myteam.balance-overhaul`

## Definition types reference

| Type | Effect | Hot-reload |
|---|---|---|
| `cameraPatch` | Adjusts orthographic camera size | Yes (live per frame) |
| `visual` | Replaces player base sprite | Yes (live per frame) |
| `localization` | Replaces text strings | Yes (new lookups) |
| `balancePatch` | Patches component stats on spawn | Partial (new objects only) |
| `introSkip` | Skips intro/splash videos on startup | Restart only |

Camera and visual changes apply live when toggled in the Mods menu.  
Balance and localization changes affect new game objects and fresh text lookups.  
Intro-skip only takes effect at next launch.

## Validation

Run `tools/package-release.ps1` from the repo root to validate your manifest and package it:

```powershell
.\tools\package-release.ps1 -ModId "yourname.my-mod"
```

See `docs/mod-makers/manifest-format.md` for the full manifest reference.

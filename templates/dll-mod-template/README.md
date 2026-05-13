# DLL Mod Template

A starter template for a **managed DLL mod** for *Everything is Crab* using Crabtya.

DLL mods run C# code at game startup through the `ICrabtyaMod` entrypoint.  
They can register UI settings, log messages, and interact with game systems.

> **Note:** DLL mods require a restart when toggled on or off in the Mods menu.  
> Content-only mods (camera, visual, etc.) change live — see the `content-mod-template`.

## What this template includes

| File | Purpose |
|---|---|
| `eicmod.json` | Mod manifest |
| `src/ModEntrypoint.cs` | Main entrypoint implementing `ICrabtyaMod` |
| `src/author.dll-mod.csproj` | C# project file referencing `Crabtya.ModApi.dll` |
| `bin/` | Output folder — DLL lands here after build |

## Quick start

### 1. Rename the template

- Rename the folder to your mod ID, e.g. `yourname.my-mod`.
- In `eicmod.json`, update `id`, `name`, `author`, `description`, and `assemblies`/`entrypoints`.
- In `src/author.dll-mod.csproj`, update `<AssemblyName>` to match your mod ID.
- In `src/ModEntrypoint.cs`, update the namespace, class name, `Id`, `Name`, `Version`.

### 2. Adjust the csproj reference path

Open `src/author.dll-mod.csproj` and update the `HintPath` for `Crabtya.ModApi` to point
to the actual installed path on your machine:

```xml
<HintPath>C:\Games\Everything is Crab\BepInEx\plugins\Crabtya.ModApi.dll</HintPath>
```

### 3. Build

```powershell
dotnet build src/author.dll-mod.csproj -c Release
```

The DLL will land at `bin/author.dll-mod.dll` — matching the path in `eicmod.json`.

### 4. Deploy

Copy the whole mod folder (including `eicmod.json` and `bin/`) into `game/Mods/`.  
Launch the game — Crabtya will discover and load your entrypoint.

## Settings API

Register settings in `OnLoad`. They appear in the **MODS** settings window in-game:

```csharp
context.Settings.RegisterBool("myToggle", "Enable X", defaultValue: true);
context.Settings.RegisterInt("count", "Count", defaultValue: 3, min: 1, max: 10, step: 1);
context.Settings.RegisterFloat("speed", "Speed", defaultValue: 1f, min: 0.5f, max: 3f, step: 0.1f);
context.Settings.RegisterOption("mode", "Mode", "Normal", new[] { "Normal", "Hard", "Easy" });
```

Read them back any time:

```csharp
if (context.Settings.TryGetBool("myToggle", out var on) && on) { ... }
```

Settings are persisted to `game/Mods/runtime-settings.json`.

## Mod lifecycle

| Method | When called |
|---|---|
| `OnLoad(context)` | Once at game startup after the loader is ready |

Additional lifecycle hooks (`OnUnload`, `OnEnable`, `OnDisable`) may be added in future
Crabtya versions.

## DLL mod caveats

- **Restart required** when toggled on or off (IL2CPP cannot unload assemblies).
- **High risk** if you patch game internals directly — use the `Crabtya.ModApi` surface only
  unless you know what you are doing.
- Keep your entrypoint constructor public and parameterless
  (`public ModEntrypoint() { }` is implied by C#).

## Packaging

```powershell
.\tools\package-release.ps1 -ModId "yourname.my-mod"
```

See `docs/mod-makers/manifest-format.md` for the full manifest reference.

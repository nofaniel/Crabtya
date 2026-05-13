# Mod Maker Quickstart (Crabtya v1)

## Starter Templates

The loader package ships ready-to-use starter templates in `templates/`:

- `templates/content-mod-template/` — a content-only mod with camera, localization, balance, visual, and intro-skip definitions pre-wired. Copy it into `game/Mods/`, rename it to your mod ID, and edit the files.
- `templates/dll-mod-template/` — a DLL mod with a C# project, entrypoint, and settings registration. Follow the README inside the template for build and deploy instructions.

Both templates include their own `README.md` with step-by-step customization instructions.

---

## 1) Content-Only Mod

Folder:

```text
game/Mods/author.content-only/
  eicmod.json
  defs/camera.json
```

Minimal manifest:

```json
{
  "id": "author.content-only",
  "name": "Content Only",
  "version": "1.0.0",
  "author": "Author",
  "description": "Content-only sample",
  "targetGame": "Everything is Crab",
  "targetUnity": "6000.2.15f1",
  "loaderVersion": "1.x",
  "dependencies": [],
  "content": {
    "catalogs": [],
    "assetBundles": [],
    "definitions": [
      "defs/camera.json"
    ]
  },
  "assemblies": [],
  "entrypoints": [],
  "defaultEnabled": true
}
```

## 2) DLL-Only Mod

Folder:

```text
game/Mods/author.dll-only/
  eicmod.json
  bin/author.dll-only.dll
```

Manifest DLL fields:

- `assemblies`: `bin/author.dll-only.dll`
- `entrypoints`: `Author.DllOnly.ModEntrypoint`

Entrypoint shape:

```csharp
using Crabtya.ModApi;

public sealed class ModEntrypoint : ICrabtyaMod
{
    public string Id => "author.dll-only";
    public string Name => "DLL Only";
    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        context.Logger.Info("Loaded.");
    }
}
```

## 3) Hybrid Mod (DLL + Content)

Folder:

```text
game/Mods/author.hybrid/
  eicmod.json
  bin/author.hybrid.dll
  defs/loc.en.json
```

Manifest includes both:

- `content.definitions` for declarative definitions;
- `assemblies` + `entrypoints` for DLL entrypoint logic.

## 4) Bundle Mod (DLL + AssetBundle)

Asset bundles let you ship Unity assets (textures, audio clips, prefabs) with your mod.

Folder:

```text
game/Mods/author.bundle-mod/
  eicmod.json
  bundles/
    my-assets          ← raw Unity AssetBundle binary (no extension)
  bin/author.bundle-mod.dll
```

Manifest:

```json
{
  "id": "author.bundle-mod",
  "content": {
    "assetBundles": ["bundles/my-assets"]
  },
  "assemblies": ["bin/author.bundle-mod.dll"],
  "entrypoints": ["Author.BundleMod.Entrypoint"]
}
```

DLL entrypoint — retrieve the bundle after startup loads it:

```csharp
using Crabtya.ModApi;
using EIC.ModLoader;
using UnityEngine;

public sealed class Entrypoint : ICrabtyaMod
{
    public string Id => "author.bundle-mod";
    public string Name => "Bundle Mod";
    public string Version => "1.0.0";

    public void OnLoad(ICrabtyaModContext context)
    {
        var bundle = AssetBundleApplicator.GetBundle("author.bundle-mod", "bundles/my-assets");
        if (bundle == null) { context.Logger.Warning("Bundle not loaded."); return; }

        var texture = bundle.LoadAsset<Texture2D>("assets/textures/my_texture.png");
        if (texture != null)
            context.Logger.Info("Texture loaded: " + texture.width + "x" + texture.height);
    }
}
```

> **Important**: bundles must be built with **Unity 6000.2.15f1** for StandaloneWindows64.
> See `docs/mod-makers/asset-workflow.md` for the full build recipe.
> Bundle-declaring mods require a restart when toggled (the toggle shows `(restart)` in the Mods menu).
> Your `.csproj` needs references to `EIC.ModLoader.dll`, `UnityEngine.CoreModule.dll`, and `UnityEngine.AssetBundleModule.dll` in addition to `Crabtya.ModApi.dll`.

## 5) Native Settings Registration

Register settings in `OnLoad`:

```csharp
context.Settings.RegisterBool("enabled", "Enabled", true);
context.Settings.RegisterInt("count", "Count", 2, 0, 10, 1);
context.Settings.RegisterFloat("scale", "Scale", 1.0f, 0.5f, 2.0f, 0.05f);
context.Settings.RegisterOption("mode", "Mode", "Default", new[] { "Default", "Hard", "Relaxed" });
```

Crabtya persists values in `game/Mods/runtime-settings.json` and attempts to render rows in native settings UI.

## 6) Validation Checklist

- `eicmod.json` parses and required fields are present.
- Paths are relative and remain under your mod folder.
- DLL mods declare both `assemblies` and `entrypoints`.
- Entrypoint type implements `ICrabtyaMod` and has public parameterless constructor.

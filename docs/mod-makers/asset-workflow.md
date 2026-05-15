# Asset Workflow

## Goals

- Allow modders to provide original content safely.
- Avoid redistribution of extracted proprietary game assets.

## Allowed Inputs

- Original modder-created assets bundled into AssetBundles.
- Optional additional Addressables catalogs authored for mod content.
- JSON definitions referenced from manifest `content.definitions`.

## Addressables and Bundles

- Place declared bundle files inside the mod folder.
- Ensure manifest paths are relative and portable.
- If a mod loads a new Addressables catalog, treat disable/remove as restart-required for full effect.

## Localization Flow

- Register localization entries via `localization` definitions.
- Keep keys namespaced by mod ID to avoid collisions.
- Validate fallback behavior for missing locale strings.
- For a concrete starter template, see `docs/mod-makers/sample-content-slice.md`.

## Safety and Legal

- Do not distribute extracted original game bundles/assets.
- Do not overwrite built-in game bundles during mod install.
- Keep all mod content self-contained under `Mods/<mod-id>/`.

---

## Building an AssetBundle for Everything is Crab

*Everything is Crab* uses **Unity 6000.2.15f1**. AssetBundles must be built with the same Unity version; bundles from other versions will fail to load at runtime.

### Prerequisites

- **Unity 6000.2.15f1** installed via Unity Hub (match the version exactly).
- The **StandaloneWindows64** build module installed with that Unity version.

### Step 1 — Create a Unity project

Open Unity Hub and create a new project using Unity 6000.2.15f1. The project type (2D, 3D, URP) does not affect bundle compatibility. A minimal 2D project is simplest.

### Step 2 — Import your assets

Drag your source files (PNG textures, WAV/OGG audio clips, prefabs, materials, etc.) into the `Assets/` folder in the Unity Editor. Do not import extracted game assets; use only original modder-created content.

### Step 3 — Assign assets to an AssetBundle

1. Select the asset in the Project window.
2. In the Inspector, find the **AssetBundle** selector at the bottom-left.
3. Click the dropdown and choose **New...** (or an existing bundle name).
4. Name the bundle to match the filename you will declare in your mod manifest, e.g. `my-assets`.

> Bundle names in Unity are **lowercased** and become the output filename. The asset paths inside the bundle are also lowercased full paths relative to `Assets/`, e.g. `assets/textures/my_texture.png`.

### Step 4 — Add an Editor build script

Create the file `Assets/Editor/BuildBundles.cs` in the Unity project:

```csharp
using UnityEditor;
using System.IO;

public static class BuildBundles
{
    [MenuItem("Assets/Build AssetBundles")]
    public static void Build()
    {
        var outputDir = "Assets/Bundles";
        Directory.CreateDirectory(outputDir);
        BuildPipeline.BuildAssetBundles(
            outputDir,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);
        UnityEngine.Debug.Log("AssetBundles built to: " + outputDir);
    }
}
```

### Step 5 — Build the bundles

1. In the Unity Editor menu, choose **Assets > Build AssetBundles**.
2. Unity generates the bundle file in `Assets/Bundles/`.
3. The output will include:
   - `my-assets` — the raw bundle binary (no extension).
   - `my-assets.manifest` — a human-readable manifest (not needed in the mod).
   - `Bundles` — a root bundle catalog (not needed in the mod).

### Step 6 — Deploy to your mod folder

Copy the raw bundle file (no extension) into your mod folder under a `bundles/` subdirectory:

```
game/Mods/author.my-mod/
  eicmod.json
  bundles/
    my-assets          ← the raw bundle binary (no extension)
```

Do **not** include `.manifest` files in your mod folder.

### Step 7 — Declare the bundle in your manifest

In `eicmod.json`:

```json
"content": {
  "assetBundles": ["bundles/my-assets"]
}
```

The path must be relative to the mod folder and may not contain `..`.

Mods that declare asset bundles are **restart-required** when toggled on or off. The toggle button in the Mods settings menu shows `(restart)` for these mods.

### Step 8 — Access assets from a DLL entrypoint

In your mod's C# entrypoint (called after the bundle is loaded at startup):

```csharp
using Crabtya.ModApi;
using UnityEngine;

var bundlePath = "bundles/my-assets";
if (context.AssetBundles.TryGetBundle(bundlePath, out _))
{
    // Asset names in bundles are lowercased full paths, e.g. "assets/textures/my_texture.png"
    var texture = context.AssetBundles.LoadAsset<Texture2D>(
        bundlePath,
        "assets/textures/my_texture.png");

    if (texture != null)
    {
        context.Logger.Info("Texture loaded: " + texture.width + "x" + texture.height);
    }

    // Optional: enumerate all loaded assets first.
    // var allAssets = context.AssetBundles.LoadAllAssets<Object>(bundlePath);
}
```

`LoadAsset<T>()` and `LoadAllAssets<T>()` are for Unity asset types (`Texture2D`, `AudioClip`, `GameObject`, etc.).
If `T` is not a Unity asset type, Crabtya logs a warning and returns no result.

> On the current Unity 6000.2.15f1 + BepInEx IL2CPP stack, prefer
> `context.AssetBundles.LoadAsset<T>()` / `LoadAllAssets<T>()` over direct
> `bundle.LoadAsset<T>()` / `bundle.LoadAllAssets()` calls.
> The direct wrappers may throw `ReadOnlySpan<T>.GetPinnableReference()` method-not-found
> exceptions on this runtime.

> **DLL project references**: to use `ICrabtyaModContext.AssetBundles` and `Texture2D`, your `.csproj` must reference:
> - `game/BepInEx/plugins/Crabtya.ModApi.dll`
> - `game/BepInEx/interop/UnityEngine.CoreModule.dll`
>
> Add extra Unity interop references only for the asset types you directly use.

### Failure paths and log output

Crabtya logs bundle-load results during startup. Check `game/BepInEx/LogOutput.log`:

| Condition | Log line |
|---|---|
| Bundle loaded successfully | `AssetBundleApplicator: loaded bundle 'bundles/my-assets' for mod 'author.my-mod'. Assets=…` |
| Bundle file not found | `AssetBundleApplicator: bundle file not found: '…' (mod='…', declared='bundles/my-assets').` |
| Wrong Unity version or incompatible bundle | `AssetBundleApplicator: LoadFromFile returned null for '…'. The bundle may have been built with a different Unity version (6000.2.15f1).` |
| Exception during load | `AssetBundleApplicator: exception loading bundle '…' for mod '…': …` |

Failed bundle loads are isolated: other mods continue loading normally.

### Troubleshooting quick table

| Symptom | Likely cause | What to do |
|---|---|---|
| `TryGetBundle("bundles/my-assets")` is `false` | Manifest path mismatch, mod disabled, or mod failed validation | Confirm `content.assetBundles` path matches exactly, then check `game/BepInEx/LogOutput.log` and `game/Mods/mod-state.json` for mod status/errors. |
| `LoadAsset<T>()` returns `null` | Wrong asset path or wrong asset type `T` | Call `GetAssetNames(...)` or `LoadAllAssets<T>()` first, then use the exact lowercased path from the bundle. |
| `GetPinnableReference()` method-not-found appears | Direct Unity wrapper call was used (`bundle.LoadAsset*` / `bundle.LoadAllAssets*`) | Use `context.AssetBundles.LoadAsset<T>()` / `LoadAllAssets<T>()` instead of direct wrapper calls. |
| Compile error for `Texture2D` / `AudioClip` / `GameObject` | Missing Unity interop reference in your DLL project | Add the matching `UnityEngine.*.dll` reference (for example `UnityEngine.CoreModule.dll` for `Texture2D`). |
| Toggle says `(restart)` and assets do not update immediately | Expected behavior for bundle-declaring mods | Restart the game after enabling/disabling the mod. |

### Quick reference

| Question | Answer |
|---|---|
| Which Unity version? | **6000.2.15f1** exactly |
| Which build target? | **StandaloneWindows64** |
| Bundle file extension? | None (raw binary, no `.bundle` suffix) |
| Where in the mod folder? | Any subdirectory; convention is `bundles/` |
| Manifest key? | `content.assetBundles: ["bundles/my-assets"]` |
| Restart required? | Yes — bundles cannot be unloaded at runtime |
| Asset name format? | Lowercased full path, e.g. `assets/textures/foo.png` |

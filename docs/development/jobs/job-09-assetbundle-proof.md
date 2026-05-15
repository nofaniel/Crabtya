# Job 09 - AssetBundle End-to-End Proof

Source PLAN follow-up: "AssetBundle end-to-end proof - requires a test bundle built with Unity 6000.2.15f1."

## Status (2026-05-14) - Done: hybrid load + mod API extraction + failure isolation proven; direct Unity wrappers still limited by IL2CPP vtable gap

Bundle startup-loading is confirmed working, programmatic asset extraction is runtime-proven through the public mod API, and missing/corrupt failure paths are now runtime-captured with isolation preserved.

**Confirmed working (runtime evidence from `game/BepInEx/LogOutput.log`):**

```
AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test' via LoadFromStream.
AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test'. Assets=assets/test_texture.png
BundleTest: bundle retrieved (path='bundles/test-assets').
BundleTest: asset count=1. Names: assets/test_texture.png
BundleTest: PASS - Texture2D 'test_texture' loaded successfully. Size=64x64.
BundleTest: PASS - context.AssetBundles.LoadAsset<Texture2D> loaded 'assets/test_texture.png'. Size=64x64.
DLL mod 'crabtya.bundle-test' entrypoint loaded: Crabtya.BundleTest.BundleTestEntrypoint
```

- Bundle loads at startup via `AssetBundle.LoadFromStream(Il2CppSystem.IO.MemoryStream)` — workaround for `LoadFromFile(string)` which throws `ReadOnlySpan.GetPinnableReference`.
- `GetAllAssetNames()` works — asset paths are enumerable.
- `ICrabtyaModContext.AssetBundles.TryGetBundle(...)` returns the loaded bundle correctly.
- `ICrabtyaModContext.AssetBundles.LoadAllAssets<T>()` and `LoadAsset<T>()` return real Unity assets from a loaded bundle.
- Hybrid load path is proven: one mod with both `content.assetBundles` and DLL entrypoint loads successfully, then extracts assets programmatically.

**Confirmed failure-path behavior (runtime evidence from `game/BepInEx/LogOutput.log`):**

```
Mod 'crabtya.bundle-missing' skipped due to validation errors: Referenced assetBundle file is missing: bundles/missing-assets
AssetBundleApplicator: LoadFromStream returned null for 'bundles/corrupt-assets' (mod='crabtya.bundle-corrupt').
AssetBundleApplicator: primary LoadFromFile threw for 'bundles/corrupt-assets' (mod='crabtya.bundle-corrupt'): Method not found: '!0 ByRef Il2CppSystem.ReadOnlySpan`1.GetPinnableReference()'.
AssetBundleApplicator: separator-normalized LoadFromFile threw for 'bundles/corrupt-assets' (mod='crabtya.bundle-corrupt'): Method not found: '!0 ByRef Il2CppSystem.ReadOnlySpan`1.GetPinnableReference()'.
AssetBundleApplicator: LoadFromFile returned null for 'bundles/corrupt-assets' (mod='crabtya.bundle-corrupt'). The bundle may have been built with a different Unity version (6000.2.15f1). Path='...\\game\\Mods\\crabtya.bundle-corrupt\\bundles\\corrupt-assets', SizeBytes=90, Header='NOT_A_UNITY_ASSET_BUNDLE
created'
AssetBundleApplicator: LoadForMods complete. Loaded=1, Errored=1, Skipped=0
DLL mod 'crabtya.bundle-test' entrypoint loaded: Crabtya.BundleTest.BundleTestEntrypoint
DLL mod 'faniel.dll-sample' entrypoint loaded: Faniel.DllSample.SampleDllEntrypoint
```

- Missing bundle declarations fail validation clearly and are isolated (`EffectiveEnabled=false` for that mod only).
- Corrupt bundle payloads now warn and isolate cleanly while other valid mods continue loading.
- `game/BepInEx/ErrorLog.log` is empty for the validated failure-path run.

**Known limitation: direct Unity wrapper calls still fail on this runtime**

Direct wrapper methods — `LoadAllAssets()`, `LoadAllAssets<T>()`, `LoadAsset<T>(string)`, `LoadAsset(string, Type)` — still throw at runtime:
```
Method not found: '!0 ByRef Il2CppSystem.ReadOnlySpan`1.GetPinnableReference()'
```

Root cause: Unity 6 IL2CPP compiles `ReadOnlySpan<T>.GetPinnableReference()` as an inlined/optimized native function not registered in the IL2CPP vtable. BepInEx's generated interop for `UnityEngine.AssetBundleModule` tries to call it via virtual dispatch (`il2cpp_object_get_virtual_method`), fails to find it, and throws. Regenerating the interop DLLs will not fix this because the method is not in the game binary's IL2CPP metadata to generate from.

This remains a BepInEx compatibility issue with Unity 6 span internals, not a loader logic bug.

**Impact on Crabtya v1:**

- **Content-only bundle mods** (where the Unity engine auto-references loaded bundle assets through scene/prefab references): unblocked — the bundle is resident in memory and the engine can use it.
- **DLL mods that programmatically load assets**: unblocked via `ICrabtyaModContext.AssetBundles`.
- **DLL mods using direct `bundle.LoadAsset(...)` / `bundle.LoadAllAssets(...)` wrappers**: still blocked until BepInEx fixes the vtable gap for Unity 6.

**What is done:**

- `loader/EIC.ModLoader/AssetBundleApplicator.cs` — `TryLoadBundle` now uses IL2CPP-safe load order:
  1. `AssetBundle.LoadFromStream(Il2CppSystem.IO.MemoryStream)` (confirmed working)
  2. `AssetBundle.LoadFromFile(string)` primary path
  3. `AssetBundle.LoadFromFile(string)` separator-normalized fallback path
- `AssetBundle.LoadFromMemory(byte[])` fallback is intentionally removed for this runtime: corrupt payloads can trigger a fatal native `AccessViolationException` before managed isolation can recover.
- `loader/EIC.ModLoader/AssetBundleApplicator.cs` — added compat extraction helpers that bypass generated span-marshalling wrappers and call injected interop safely:
  - `LoadAssetCompat(AssetBundle, string, Il2CppSystem.Type)`
  - `LoadAssetCompat<T>(AssetBundle, string)`
  - `LoadAllAssetsCompat(AssetBundle)`
  - `LoadAllAssetsCompat<T>(AssetBundle)`
- `loader/Crabtya.ModApi/ICrabtyaAssetBundleRegistry.cs` and `ICrabtyaModContext.AssetBundles` now expose a clean, loader-owned bundle API for mods (`TryGetBundle`, `GetAssetNames`, `LoadAsset<T>`, `LoadAllAssets<T>`).
- `game/Mods/crabtya.bundle-test/src/BundleTestEntrypoint.cs` — switched proof path to compat helpers and captured runtime PASS for both all-assets and direct-name extraction.
- Added reusable failure fixtures:
  - `game/Mods/crabtya.bundle-missing/`
  - `game/Mods/crabtya.bundle-corrupt/`
- Full Unity 6000.2.15f1 bundle build recipe documented in `docs/mod-makers/asset-workflow.md`.

**What remains:**

1. Keep direct-wrapper limitation documented until BepInEx ships a Unity 6 span-vtable fix.

## Objective

Prove the `content.assetBundles` pipeline works end to end with a real Unity-built bundle for
*Everything is Crab*'s engine version (`6000.2.15f1`). Loading code already exists in
`AssetBundleApplicator`; this Job verifies it against a real artifact, documents the mod-maker
build steps, and surfaces any gaps that block the v1 contract.

## Scope

Work in `loader/EIC.ModLoader/AssetBundleApplicator.cs` and `loader/Crabtya.ModApi/` only as
needed to expose retrieval helpers, plus a dedicated test mod under `game/Mods/<test-mod-id>/`,
the DLL template under `templates/dll-mod-template/` if changes are needed, and mod-maker docs
under `docs/mod-makers/`. The Unity test bundle itself is an external artifact built for the
target editor version and consumed by this Job. Do not check large binary bundles into source
control without explicit user confirmation.

## Implementation Plan

1. Confirm the engine version requirement (`Unity 6000.2.15f1`) and document the Unity project setup needed to produce a `StandaloneWindows64` bundle compatible with IL2CPP runtime loading. **Done** (`docs/mod-makers/asset-workflow.md`).
2. Obtain or build one minimal test bundle containing at least one named asset and drop it under `game/Mods/crabtya.bundle-test/bundles/test-assets`. **Done locally (2026-05-14): bundle built with Unity 6000.2.15f1 and staged for runtime verification.**
3. Author a minimal hybrid mod that declares `content.assetBundles` in `eicmod.json`, loads the bundle through `ICrabtyaModContext.AssetBundles`, and applies one visible effect. **Done** (`game/Mods/crabtya.bundle-test/`).
4. Walk the full flow once: enable in mod menu, restart, confirm `LogOutput.log` records the bundle load, and confirm the asset is retrieved and logged. **Done for load + retrieval + hybrid entrypoint + mod API extraction (`LoadAllAssets<T>`, `LoadAsset<T>`).**
5. Exercise failure paths: missing bundle, corrupt bundle, and restart-required toggle queueing. **Done (runtime evidence captured for both missing and corrupt paths, plus restart-required behavior).**
6. Update `docs/mod-makers/manifest-format.md`, quickstart, and asset-workflow with bundle build recipe, naming/path expectations, and `GetBundle` retrieval pattern. **Done.**
7. Update `PLAN.md` "Recently landed" to move AssetBundle from "loading exists" to "end-to-end proven" once verification is captured. **Done for compat extraction path; direct-wrapper limitation is documented.**

## Deliverables

- One verified test bundle plus a minimal hybrid test mod that proves load, retrieval, and use. **Done for load/retrieval/use through compat extraction helpers; direct Unity wrappers remain a known runtime limitation.**
- Documented Unity build recipe targeting `Unity 6000.2.15f1` and `StandaloneWindows64`. **Done.**
- Updated mod-maker docs covering manifest declaration, retrieval API, and restart-required behavior. **Done.**
- Runtime evidence captured for the success path and at least two failure paths. **Done.**

## Verification

- Build: `dotnet build game/Mods/crabtya.bundle-test/src/crabtya.bundle-test.csproj -c Release`. **Done: 0 errors, 0 warnings.**
- Runtime: bundle load line appears in `LogOutput.log` with the expected key; asset retrieval returns a non-null Unity object; the result is logged clearly. **Done for bundle load + `AssetBundles.TryGetBundle(...)` retrieval + `LoadAllAssets<T>()` / `LoadAsset<T>()` extraction.**
- Runtime: missing or corrupt bundle declarations log a clear isolation warning and do not block other mods. **Done.**
- Runtime: toggling the bundle-declaring mod queues a restart command in `startup-commands.json` instead of hot-applying, consistent with `LiveModRegistry.RequiresRestart`. **Done.**
- `game/BepInEx/ErrorLog.log` remains empty for the validated run. **Done (empty in latest validated run, including failure-path run).**

## Completion Criteria

- Crabtya has runtime evidence that real Unity bundles built for the target engine version load and surface assets to mods.
- Mod makers have enough docs to build their own bundle without reading loader source.
- `PLAN.md` no longer lists AssetBundle end-to-end proof as a planned follow-up outside the Job board.

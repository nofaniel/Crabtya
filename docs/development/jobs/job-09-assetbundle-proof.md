# Job 09 - AssetBundle End-to-End Proof

Source PLAN follow-up: "AssetBundle end-to-end proof - requires a test bundle built with Unity 6000.2.15f1."

## Status (2026-05-14) - Infrastructure proven; asset-extraction blocked by IL2CPP vtable gap

Bundle startup-loading is confirmed working. All programmatic asset-extraction methods (`LoadAsset`, `LoadAllAssets`, all typed/untyped/generic variants) are blocked by a Unity 6 / BepInEx IL2CPP interop limitation.

**Confirmed working (runtime evidence from `game/BepInEx/LogOutput.log`):**

```
AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test' via LoadFromStream.
AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test'. Assets=assets/test_texture.png
BundleTest: bundle retrieved (key=crabtya.bundle-test:bundles/test-assets).
BundleTest: asset count=1. Names: assets/test_texture.png
```

- Bundle loads at startup via `AssetBundle.LoadFromStream(Il2CppSystem.IO.MemoryStream)` — workaround for `LoadFromFile(string)` which throws `ReadOnlySpan.GetPinnableReference`.
- `GetAllAssetNames()` works — asset paths are enumerable.
- `AssetBundleApplicator.GetBundle(modId, path)` returns the loaded bundle correctly.

**Blocked: programmatic asset extraction (known BepInEx / Unity 6 issue)**

All asset-load methods — `LoadAllAssets()`, `LoadAllAssets<T>()`, `LoadAsset<T>(string)`, `LoadAsset(string, Type)` — throw at runtime:
```
Method not found: '!0 ByRef Il2CppSystem.ReadOnlySpan`1.GetPinnableReference()'
```

Root cause: Unity 6 IL2CPP compiles `ReadOnlySpan<T>.GetPinnableReference()` as an inlined/optimized native function not registered in the IL2CPP vtable. BepInEx's generated interop for `UnityEngine.AssetBundleModule` tries to call it via virtual dispatch (`il2cpp_object_get_virtual_method`), fails to find it, and throws. Regenerating the interop DLLs will not fix this because the method is not in the game binary's IL2CPP metadata to generate from.

This is a BepInEx compatibility issue with Unity 6 span internals, not a loader bug.

**Impact on Crabtya v1:**

- **Content-only bundle mods** (where the Unity engine auto-references loaded bundle assets through scene/prefab references): unblocked — the bundle is resident in memory and the engine can use it.
- **DLL mods that programmatically load assets** (`AssetBundleApplicator.GetBundle(...).LoadAsset(...)`): blocked until BepInEx fixes the vtable gap for Unity 6.

**What is done:**

- `loader/EIC.ModLoader/AssetBundleApplicator.cs` — `TryLoadBundle` now tries IL2CPP-safe load order:
  1. `AssetBundle.LoadFromStream(Il2CppSystem.IO.MemoryStream)` (confirmed working)
  2. `AssetBundle.LoadFromMemory(byte[])` (throws GC error on this build — kept as fallback)
  3. `AssetBundle.LoadFromFile(string)` × 2 path variants (both throw GetPinnableReference — kept for diagnostics)
- `game/Mods/crabtya.bundle-test/src/BundleTestEntrypoint.cs` — probed `LoadAllAssets`, `LoadAllAssets<T>()`, and `LoadAsset<T>(string)` variants; all confirmed blocked; code left in place for when the BepInEx fix lands.
- Full Unity 6000.2.15f1 bundle build recipe documented in `docs/mod-makers/asset-workflow.md`.

**What remains (blocked, not actionable in current BepInEx):**

1. `BundleTest: PASS - Texture2D 'test_texture' loaded successfully.` — requires BepInEx fix for `ReadOnlySpan<T>.GetPinnableReference()` vtable registration on Unity 6.
2. Failure-path checks (missing bundle, corrupt bundle) — can be run now if desired; they only exercise the load path (which works) not asset extraction.
3. Restart-required toggle: enable or disable `crabtya.bundle-test` in Mods menu; verify queue writes to `startup-commands.json`.

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
3. Author a minimal hybrid mod that declares `content.assetBundles` in `eicmod.json`, loads the bundle through `AssetBundleApplicator.GetBundle`, and applies one visible effect. **Done** (`game/Mods/crabtya.bundle-test/`).
4. Walk the full flow once: enable in mod menu, restart, confirm `LogOutput.log` records the bundle load, and confirm the asset is retrieved and logged. **Pending runtime verification.**
5. Exercise failure paths: missing bundle, corrupt bundle, and restart-required toggle queueing. **Pending runtime verification.**
6. Update `docs/mod-makers/manifest-format.md`, quickstart, and asset-workflow with bundle build recipe, naming/path expectations, and `GetBundle` retrieval pattern. **Done.**
7. Update `PLAN.md` "Recently landed" to move AssetBundle from "loading exists" to "end-to-end proven" once verification is captured. **Pending runtime verification.**

## Deliverables

- One verified test bundle plus a minimal hybrid test mod that proves load, retrieval, and use. **Bundle staged locally; runtime proof still pending.**
- Documented Unity build recipe targeting `Unity 6000.2.15f1` and `StandaloneWindows64`. **Done.**
- Updated mod-maker docs covering manifest declaration, retrieval API, and restart-required behavior. **Done.**
- Runtime evidence captured for the success path and at least two failure paths. **Pending.**

## Verification

- Build: `dotnet build game/Mods/crabtya.bundle-test/src/crabtya.bundle-test.csproj -c Release`. **Done: 0 errors, 0 warnings.**
- Runtime: bundle load line appears in `LogOutput.log` with the expected key; asset retrieval returns a non-null Unity object; the result is logged clearly. **Pending.**
- Runtime: missing or corrupt bundle declarations log a clear isolation warning and do not block other mods. **Pending.**
- Runtime: toggling the bundle-declaring mod queues a restart command in `startup-commands.json` instead of hot-applying, consistent with `LiveModRegistry.RequiresRestart`. **Pending.**
- `game/BepInEx/ErrorLog.log` remains empty for the validated run. **Pending.**

## Completion Criteria

- Crabtya has runtime evidence that real Unity bundles built for the target engine version load and surface assets to mods.
- Mod makers have enough docs to build their own bundle without reading loader source.
- `PLAN.md` no longer lists AssetBundle end-to-end proof as a planned follow-up outside the Job board.

# Job 09 - AssetBundle End-to-End Proof

Source PLAN follow-up: "AssetBundle end-to-end proof - requires a test bundle built with Unity 6000.2.15f1."

## Status (2026-05-14) - In progress: real test bundle staged, runtime proof pending

Loader-side implementation and all supporting infrastructure is complete. A real Unity `6000.2.15f1`
`StandaloneWindows64` test bundle is now staged locally. Runtime success-path proof still requires
an in-game validation run and the documented failure-path checks.

**What is done:**

- `game/Mods/crabtya.bundle-test/` - hybrid test mod:
  - `eicmod.json` - declares `content.assetBundles: ["bundles/test-assets"]` and DLL entrypoint `Crabtya.BundleTest.BundleTestEntrypoint`.
  - `src/BundleTestEntrypoint.cs` - retrieves the bundle via `AssetBundleApplicator.GetBundle()`, lists all asset names, and attempts to load `Texture2D` named `test_texture`. Results are logged clearly for success and failure cases.
  - `src/crabtya.bundle-test.csproj` - references `Crabtya.ModApi.dll`, `EIC.ModLoader.dll`, and Unity interop assemblies. Output goes to `bin/`.
  - `bin/crabtya.bundle-test.dll` - built with 0 errors and 0 warnings.
  - `bundles/test-assets` - real Unity `6000.2.15f1` `StandaloneWindows64` bundle staged locally with a generated `test_texture` asset.
- `docs/mod-makers/asset-workflow.md` - full Unity `6000.2.15f1` bundle build recipe: project setup, asset bundle assignment, editor build script, output deployment, DLL access, and failure log table.
- `docs/mod-makers/manifest-format.md` - `content.assetBundles` moved into the active v1 surface list; validation rules updated with retrieval pattern and link to asset workflow.
- `docs/mod-makers/quickstart.md` - new bundle-mod section with folder layout, manifest snippet, and DLL entrypoint example.

**What remains:**

1. Runtime verification - success path:
   - Enable `crabtya.bundle-test` in the Mods menu. The toggle should show `(restart)`.
   - Restart the game.
   - Confirm `LogOutput.log` contains `AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test'.`
   - Confirm `LogOutput.log` contains `BundleTest: PASS - Texture2D 'test_texture' loaded successfully.`
2. Runtime verification - failure paths:
   - Missing bundle: remove or rename the bundle file, restart, and verify the `bundle file not found` warning while other mods continue loading.
   - Corrupt bundle: replace the file with a text file, restart, and verify `LoadFromFile returned null` while other mods continue loading.
   - Restart-required toggle: enable or disable `crabtya.bundle-test` in Mods menu and verify the queue writes to `startup-commands.json`.
3. Confirm `game/BepInEx/ErrorLog.log` stays empty for the validated run.
4. Update this file and `PLAN.md` with runtime evidence once captured.

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

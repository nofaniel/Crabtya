# Job 09 - AssetBundle End-to-End Proof

Source PLAN follow-up: "AssetBundle end-to-end proof - requires a test bundle built with Unity 6000.2.15f1."

## Status (2026-05-13) — In progress: infrastructure complete, runtime proof pending

Loader-side implementation and all supporting infrastructure is complete. Runtime success-path proof
is gated on supplying a real Unity 6000.2.15f1 StandaloneWindows64 bundle file.

**What is done:**

- `game/Mods/crabtya.bundle-test/` — hybrid test mod:
  - `eicmod.json` — declares `content.assetBundles: ["bundles/test-assets"]` and DLL entrypoint `Crabtya.BundleTest.BundleTestEntrypoint`.
  - `src/BundleTestEntrypoint.cs` — retrieves the bundle via `AssetBundleApplicator.GetBundle()`, lists all asset names, and attempts to load `Texture2D` named `test_texture`. Results are logged clearly (success and failure cases).
  - `src/crabtya.bundle-test.csproj` — references `Crabtya.ModApi.dll`, `EIC.ModLoader.dll`, Unity interop assemblies. Output to `bin/`.
  - `bin/crabtya.bundle-test.dll` — built 0 errors / 0 warnings.
  - `bundles/` directory exists; user drops bundle file here.
- `docs/mod-makers/asset-workflow.md` — full Unity 6000.2.15f1 bundle build recipe: project setup, asset bundle assignment, Editor build script, output deployment, DLL access, failure log table.
- `docs/mod-makers/manifest-format.md` — `content.assetBundles` moved from "possible but not final-stable" to the active v1 surface list; validation rules section updated with retrieval pattern and link to asset-workflow.
- `docs/mod-makers/quickstart.md` — new Section 4 "Bundle Mod" with folder layout, manifest snippet, and DLL entrypoint example.

**What remains (user-gated):**

1. **Supply the test bundle**: build a Unity 6000.2.15f1 StandaloneWindows64 bundle named `test-assets` containing a `Texture2D` asset named `test_texture`. Place the file at `game/Mods/crabtya.bundle-test/bundles/test-assets`.
2. **Runtime verification — success path**:
   - Enable `crabtya.bundle-test` in the Mods menu (toggle shows `(restart)`).
   - Restart the game.
   - Confirm `LogOutput.log` contains: `AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test'. Assets=…`
   - Confirm `LogOutput.log` contains: `BundleTest: PASS - Texture2D 'test_texture' loaded successfully.`
3. **Runtime verification — failure paths**:
   - **Missing bundle**: remove/rename the bundle file, restart → verify `AssetBundleApplicator: bundle file not found` warning in log, other mods unaffected.
   - **Corrupt bundle**: replace with a text file, restart → verify `LoadFromFile returned null` warning, other mods unaffected.
   - **Restart-required toggle**: enable/disable `crabtya.bundle-test` in Mods menu → verify toggle shows `(restart)`, command queued to `startup-commands.json`.
4. Confirm `game/BepInEx/ErrorLog.log` stays empty for the validated run.
5. Update this file and `PLAN.md` with runtime evidence once captured.

## Objective

Prove the `content.assetBundles` pipeline works end to end with a real Unity-built bundle for *Everything is Crab*'s engine version (Unity 6000.2.15f1). Loading code already exists in `AssetBundleApplicator`; this Job verifies it against a real artifact, documents the mod-maker build steps, and surfaces any gaps that block the v1 contract.

## Scope

Work in `loader/EIC.ModLoader/AssetBundleApplicator.cs` and `loader/Crabtya.ModApi/` only as needed to expose retrieval helpers, plus a dedicated test mod under `game/Mods/<test-mod-id>/`, the DLL template under `templates/dll-mod-template/` if changes are needed, and mod-maker docs under `docs/mod-makers/`. The Unity test bundle itself is an external artifact built by the user against Unity 6000.2.15f1; this Job consumes it. Do not check large binary bundles into source control without explicit user confirmation; prefer `.gitignore` plus a documented build recipe.

## Implementation Plan

1. Confirm the engine version requirement (`Unity 6000.2.15f1`) and document the Unity project setup needed to produce a Windows StandaloneWindows64 bundle compatible with IL2CPP runtime loading. **Done** (`docs/mod-makers/asset-workflow.md`).
2. Coordinate with the user to obtain (or build) one minimal test bundle containing at least one named asset, e.g. a `Texture2D` or `Sprite` with a known asset name. Drop it under a test mod folder such as `game/Mods/crabtya.bundle-test/bundles/test-assets`. **Infrastructure ready; bundle file user-gated.**
3. Author a minimal hybrid mod that declares `content.assetBundles` in `eicmod.json`, loads the bundle through `AssetBundleApplicator.GetBundle`, and applies one visible effect. Keep this mod isolated from release sample mods. **Done** (`game/Mods/crabtya.bundle-test/`).
4. Walk the full flow once: enable in mod menu, restart, confirm `LogOutput.log` records the bundle load, confirm the asset is retrieved and logged. **Pending runtime verification.**
5. Exercise failure paths: a missing bundle path, a corrupt bundle, and a bundle-declaring mod's restart-required toggle. Confirm each case is isolated and writes a clear log line. **Pending runtime verification.**
6. Update `docs/mod-makers/manifest-format.md`, quickstart, and asset-workflow with the bundle build recipe, naming/path expectations, and the `GetBundle` retrieval pattern. **Done.**
7. Update `PLAN.md` "Recently landed" to move AssetBundle from "loading exists" to "end-to-end proven" once verification is captured. **Pending runtime verification.**

## Deliverables

- One verified test bundle plus a minimal hybrid test mod that proves load + retrieval + use. **Test mod complete; bundle user-gated.**
- Documented Unity build recipe targeting `Unity 6000.2.15f1` and `StandaloneWindows64`. **Done.**
- Updated mod-maker docs covering manifest declaration, retrieval API, and restart-required behavior. **Done.**
- Runtime evidence captured for the success path and at least two failure paths. **Pending.**

## Verification

- Build: `dotnet build game/Mods/crabtya.bundle-test/src/crabtya.bundle-test.csproj -c Release`. **Done: 0 errors, 0 warnings.**
- Runtime: bundle load line appears in `LogOutput.log` with the expected key; asset retrieval returns a non-null Unity object; the result is logged clearly. **Pending.**
- Runtime: missing/corrupt bundle declarations log a clear isolation warning and do not block other mods. **Pending.**
- Runtime: toggling the bundle-declaring mod queues a restart command (`startup-commands.json`) instead of hot-applying, consistent with `LiveModRegistry.RequiresRestart`. **Pending.**
- `game/BepInEx/ErrorLog.log` remains empty for the validated run. **Pending.**

## Completion Criteria

- Crabtya has runtime evidence that real Unity bundles built for the target engine version load and surface assets to mods.
- Mod makers have enough docs to build their own bundle without reading loader source.
- `PLAN.md` no longer lists AssetBundle end-to-end proof as a planned follow-up outside the Job board.

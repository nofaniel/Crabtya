# Crabtya v1 Handover

Date: 2026-05-14

Purpose
- Provide a concise, actionable handover for the Crabtya v1 work that just landed (Jobs 01-10) and the Job 11 kickoff (Crabtya Lite scaffold).

Executive summary
- Job 09 (AssetBundle proof) and Job 10 (v1 validation & packaging readiness) are complete and runtime-evidenced.
- Job 11 (Crabtya Lite) has been scaffolded, compiles, and includes coexistence guards; final validation and packaging remain.
- A safe, API-first surface for mod authors to load asset bundles is available via ICrabtyaModContext.AssetBundles.

Scope of this handover
- What landed in code and docs
- How to verify locally and in CI-like runs
- Known issues, mitigations, and recommended next steps to finish Job 11 and release

What landed (high level)
- Loader improvements and safety hardening
  - loader/EIC.ModLoader/AssetBundleApplicator.cs: compatibility helpers for Unity6 IL2CPP interop; removed unsafe LoadFromMemory fallback; primary LoadFromStream path.
  - loader/EIC.ModLoader/DllModLoader.cs: wiring the Crabtya asset-bundle registry into mod contexts.
- Public mod API updates
  - loader/Crabtya.ModApi/ICrabtyaAssetBundleRegistry.cs (new)
  - loader/Crabtya.ModApi/ICrabtyaModContext.cs (AssetBundles property added)
- Sample and test fixtures
  - game/Mods/crabtya.bundle-test/ (example hybrid mod updated to use AssetBundles API)
  - game/Mods/crabtya.bundle-missing/ and game/Mods/crabtya.bundle-corrupt/ (deterministic failure fixtures)
- Crabtya Lite scaffold (Job 11 kickoff)
  - loader/Crabtya.Lite/* (project compiles; includes runtime bootstrap and settings persistence)
- Packaging & tooling
  - tools/package-release.ps1 updated to support creating Lite packages and new flags
  - tools/Install-CrabtyaLite.ps1 and tools/Uninstall-CrabtyaLite.ps1 added
- Documentation
  - docs/* updated: job notes, mod-maker guidance, quickstart, manifest format, and Job-specific proof files

Runtime evidence and artifacts
- Primary runtime log (after launching the game with BepInEx and the built plugin):
  - game/BepInEx/LogOutput.log — contains loader startup lines, plugin fingerprint, AssetBundleApplicator messages, sample mod evidence
  - game/BepInEx/ErrorLog.log — validated empty in test runs (empty is expected)
- Run-state files produced by loader
  - game/Mods/mod-state.json
  - game/Mods/startup-commands.json (consumed on start in validated runs)
  - game/Mods/runtime-settings.json (loader-owned settings persisted)

Representative log lines (search LogOutput.log for these substrings):
- "Plugin binary fingerprint" — verify the loaded DLL matches the built artifact
- "AssetBundleApplicator: loaded bundle" — bundle success via LoadFromStream
- "LoadFromStream returned null" / "Referenced assetBundle file is missing" — failure path isolation messages for corrupt/missing bundles
- "Queued startup mod-toggle commands consumed" — startup toggle evidence

How to verify locally (quick checklist)
1. Build the loader and lite scaffold:
   - dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release
   - dotnet build loader/Crabtya.Lite/Crabtya.Lite.csproj -c Release
2. Deploy the built plugin into the local game install (build places DLLs where the project file targets; runtime build process in repo already deploys to game/BepInEx/plugins during CI-like runs). If locked, close the game and retry.
3. Launch the game with the loader-enabled launcher: game\__LAUNCHER.bat or run game\Everything is Crab.exe -timestamps
4. Inspect logs in game/BepInEx/LogOutput.log for the representative lines above.
5. Confirm ErrorLog.log is empty.
6. Verify sample mods loaded:
   - game/Mods/crabtya.bundle-test should log its PASS line (asset count/name and LoadAssetCompat usage).
7. Test failure fixtures:
   - Put crabtya.bundle-missing and crabtya.bundle-corrupt under game/Mods and restart. Confirm loader logs missing/corrupt diagnostics and other mods still load.

Developer verification commands (PowerShell examples)
- Build both projects (from repo root):
  - dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release
  - dotnet build loader/Crabtya.Lite/Crabtya.Lite.csproj -c Release
- Tail logs after launch (example):
  - Get-Content -LiteralPath game\BepInEx\LogOutput.log -Wait -Tail 200

Known issues and mitigations
- AssetBundle.LoadFromMemory(byte[]) caused native AccessViolationException on corrupt payloads in current Unity6 IL2CPP runtime. Mitigation: removed fallback; use LoadFromStream as the primary path and LoadFromFile only as diagnostic fallback. Keep bundle fixture tests.
- Direct bundle.LoadAsset*/LoadAllAssets* wrappers may fail due to a ReadOnlySpan<T>.GetPinnableReference() interop mismatch in this IL2CPP/BepInEx environment. Mitigation: use loader-provided compat helpers (AssetBundleApplicator.LoadAssetCompat/LoadAllAssetsCompat) or the public ICrabtyaAssetBundleRegistry API which exposes safe helpers.
- Crabtya Lite is intentionally isolated and will abort if the full loader is present. This avoids mixed installs where both attempt runtime responsibilities.

Open questions for the next owner
- Do we want to allow an explicit user override to force Lite + full-loader coexistence in release packages? Right now the repo includes an AllowLiteCoexist/SkipLite flag intended only for local testing.
- Is there appetite to reintroduce a memory-based bundle path after upstream IL2CPP span/vtable fixes? Recommendation: keep the memory path disabled until Unity/BepInEx provide a compatible interop layer.

Next steps (recommended)
1. Produce the actual Lite release ZIP using tools/package-release.ps1 New-LitePackage flag; capture package SHA256 and store under packages/lite/.
2. Run the full runtime validation matrix for Lite-only installs: multiple resolutions, input devices, and a small curated mods set.
3. Finalize user-facing docs comparing Lite vs full Crabtya and add an explicit incompatibility note.
4. Create PR with all changes (code + docs) and ask reviewers: loader owners, packaging owner, and QA to run the acceptance checklist in docs/development/test-checklist.md.
5. After PR merges, tag release and publish artifacts.

Handover ownership
- Primary implementer: repository worktree (see commit histories) — use the contributor log to identify exact author commits.
- Recommended reviewers / handoff recipients:
  - Loader runtime owner
  - Mod API owner
  - Packaging/Release owner
  - QA engineer for runtime acceptance tests

Where to look first (file map)
- Loader runtime: loader/EIC.ModLoader/
- Mod API: loader/Crabtya.ModApi/
- Sample mods and fixtures: game/Mods/
- Lite scaffold: loader/Crabtya.Lite/
- Packaging scripts: tools/
- Docs: docs/development/, docs/mod-makers/, docs/users/

Contact / notes
- If something fails during verification, capture full LogOutput.log and ErrorLog.log and attach them to the PR/issue. Do not attempt to re-enable LoadFromMemory fallback until the underlying AccessViolation cause is investigated against a reproducible corrupt bundle payload.

End of handover

# Job 10 - Full v1 Validation Pass and Release Packaging

Source PLAN follow-up: "Full v1 validation pass and release packaging."

## Status (2026-05-14) — Done: release packaging complete, 15/15 checklist items evidenced

**Packaging pass complete (2026-05-13):**

- `tools/package-release.ps1` fixed for `Set-StrictMode -Version Latest` PSCustomObject property access (all direct `$manifest.xxx` accesses replaced with `Get-ManifestString` helper or explicit `PSObject.Properties` guards).
- `tools/package-release.ps1` extended with `-SkipTemplates` flag and `New-TemplatePackage` function — now produces separate template zips under `packages/templates/`.
- Fresh v1.0.0 artifacts built:
  - `packages/loader/EverythingIsCrab.ModLoader-v1.0.0.zip` — 266752-byte plugin (covers all Jobs 01-09 code changes)
  - `packages/mods/faniel.dll-sample-1.0.0.zip` — 0 warnings
  - `packages/mods/faniel.fov-slider-1.0.0.zip` — 0 warnings
  - `packages/mods/faniel.mousewheel-zoom-1.0.0.zip` — 0 warnings
  - `packages/mods/faniel.mvp-sample-1.0.0.zip` — 0 warnings
  - `packages/mods/faniel.no-intro-1.0.0.zip` — 0 warnings
  - `packages/mods/faniel.ui-scale-1.0.0.zip` — 0 warnings
  - `packages/templates/content-mod-template-v1.0.0.zip`
  - `packages/templates/dll-mod-template-v1.0.0.zip`
- Test fixtures (`test.*`, `crabtya.bundle-test`, `persist.test`) intentionally excluded from release packages.
- `packages/README.md` rewritten with artifact descriptions, install instructions, fingerprint verification steps, and regeneration command.
- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` — 0 errors, 0 warnings.

**Checklist closure evidence (runtime):**

Latest runtime evidence source: `game/BepInEx/LogOutput.log` (2026-05-14 launch), plus prior Job 10 validation logs for already-closed UI/settings/hot-toggle items.

- `Queued startup mod-toggle commands consumed: 1`
- `Startup mod-toggle commands detected: 1`
- `Startup toggle applied for 'faniel.dll-sample': Enabled=True`
- `Discovery complete ... StartupToggles=1, StartupQueue=1, StartupResolved=1 ...`
- `game/Mods/startup-commands.json` absent after startup (consumed/cleared)
- `game/Mods/mod-state.json` shows `StartupToggles.QueueFileConsumed=true`, `QueuedToggleCount=1`, `ResolvedFinalIntents=["faniel.dll-sample=enable(mods_window)"]`

| # | Item | Status |
|---|---|---|
| 1 | Plugin fingerprint line proves deployed DLL provenance | Done (runtime evidence captured) |
| 2 | Valid content-only mod loads | Done (runtime evidence captured) |
| 3 | Valid DLL-only mod loads | Done (runtime evidence captured) |
| 4 | Valid hybrid mod loads | Done (runtime evidence captured for bundle load + retrieval + DLL entrypoint) |
| 5 | Broken manifest or missing DLL is isolated | Done (runtime evidence captured) |
| 6 | Throwing DLL entrypoint is isolated | Done (runtime evidence captured) |
| 7 | Main menu shows Crabtya label and injected native-styled `MODS` button | Done (runtime evidence captured) |
| 8 | `MODS` opens dedicated native-style Mods settings menu | Done (runtime evidence captured) |
| 9 | Pause/start menu `MOD SETTINGS` opens same Mods settings menu | Done (runtime evidence captured) |
| 10 | Settings interactions persist to `runtime-settings.json` | Done (runtime evidence captured) |
| 11 | Enable/disable toggles queue to `startup-commands.json` and apply next launch | Done (runtime evidence captured) |
| 12 | Content-only toggles hot-apply without writing a restart command | Done (runtime evidence captured) |
| 13 | `ErrorLog.log` remains empty for the validated run | Done (runtime evidence captured) |
| 14 | Achievement unlock attempts are blocked | Done (runtime evidence captured) |
| 15 | Save files land in `CrabtyaData/IsolatedSave/` and do not mutate base save | Done (runtime evidence captured) |

Residual follow-ups (outside Job 10 checklist closure):
- Job 09 direct Unity wrapper extraction (`bundle.LoadAsset*` / `bundle.LoadAllAssets*`) remains limited by known BepInEx/Unity 6 IL2CPP `ReadOnlySpan<T>.GetPinnableReference()` interop gap; loader mod API extraction and missing/corrupt failure-path isolation are runtime-proven.

## Objective

Run the authoritative v1 validation checklist end to end against the current build, fix anything it surfaces, and produce the final release packaging artifacts that Job 02 will publish. This Job is the gate between "code looks done" and "ready to ship".

## Scope

Read-and-verify across the entire repo, then concentrate write work in `packages/`, `tools/Install-Crabtya.ps1`, `tools/Uninstall-Crabtya.ps1`, root release docs, and any small loader fixes the validation pass forces. Do not modify original game binaries or bundled assets under `game/Everything is Crab_Data/`. Do not expand scope into new features; this Job is verification, packaging, and tightly-scoped fixes only.

## Implementation Plan

1. Freeze feature work for the duration of this Job. Any new fix must be justified by a failing checklist item or a release-blocking defect.
2. Execute every item in the `PLAN.md` v1 Validation Checklist (currently 15 items, covering plugin fingerprint, content/DLL/hybrid load paths, broken-mod isolation, menu surfaces, settings persistence, hot-toggle vs queued restart behavior, achievement blocking, and isolated-save IO). Capture evidence from `game/BepInEx/LogOutput.log`, `game/BepInEx/ErrorLog.log`, `game/Mods/mod-state.json`, `game/Mods/startup-commands.json`, `game/Mods/runtime-settings.json`, and `game/CrabtyaData/IsolatedSave/` as applicable.
3. For each failed item, file the smallest possible fix, re-run only the items it affects, and record the before/after in this Job plan.
4. Re-run the focused mod scenarios from Jobs 06 and 07, the conflict scenarios from Job 05, the enemy stat scenario from Job 08, and the AssetBundle scenario from Job 09 so the release captures their proof too.
5. Produce release packaging:
   - Versioned loader zip under `packages/` with `tools/Install-Crabtya.ps1` and `tools/Uninstall-Crabtya.ps1`.
   - Mod template zips (`templates/content-mod-template/`, `templates/dll-mod-template/`) at the same version.
   - A `packages/README.md` update describing what is in each artifact and how to verify its fingerprint at runtime.
6. Confirm the release artifacts install cleanly into a fresh game directory, the plugin fingerprint line matches, and a smoke run loads at least one content-only mod and one DLL mod.
7. Update `docs/development/agent-handoff.md` with the validation result and a list of any residual risks deferred to post-release.

## Deliverables

- Completed v1 validation checklist with evidence per item recorded in this Job plan or a referenced log.
- Final loader and template release zips under `packages/` at a single coherent version string.
- Updated `packages/README.md` describing the artifacts and verification steps.
- Handoff note summarizing release-readiness and any residual risks.

## Verification

- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` clean.
- Every v1 Validation Checklist item passes with captured evidence; failures are either fixed or explicitly waived with rationale.
- Fresh-install dry run: extract the loader zip into a clean game directory, run the installer, launch the game, and confirm the plugin fingerprint plus the modded boot path.
- `game/BepInEx/ErrorLog.log` empty across all validated runs.

## Completion Criteria

- Crabtya v1 has a single, reproducible release set under `packages/` matching a clean validation pass.
- Anything blocking publication is fixed or explicitly deferred with a written reason.
- `PLAN.md` no longer lists v1 validation or release packaging as a planned follow-up outside the Job board, leaving Job 02 to handle git/GitHub publication only.

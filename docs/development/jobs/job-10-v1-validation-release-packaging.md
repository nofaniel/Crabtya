# Job 10 - Full v1 Validation Pass and Release Packaging

Source PLAN follow-up: "Full v1 validation pass and release packaging."

## Status (2026-05-13) — In progress: release packaging complete, validation checklist runtime-gated

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

**What remains (runtime-gated — requires running the game):**

The full v1 Validation Checklist (15 items) requires a live game run. Items that can be checked statically are noted below; all others require runtime evidence from `LogOutput.log` / `ErrorLog.log`.

| # | Item | Status |
|---|---|---|
| 1 | Plugin fingerprint line proves deployed DLL provenance | Pending runtime |
| 2 | Valid content-only mod loads | Pending runtime |
| 3 | Valid DLL-only mod loads | Pending runtime |
| 4 | Valid hybrid mod loads | Pending runtime |
| 5 | Broken manifest or missing DLL is isolated | Pending runtime |
| 6 | Throwing DLL entrypoint is isolated | Pending runtime |
| 7 | Main menu shows Crabtya label and injected native-styled `MODS` button | Pending runtime |
| 8 | `MODS` opens dedicated native-style Mods settings menu | Pending runtime |
| 9 | Pause/start menu `MOD SETTINGS` opens same Mods settings menu | Pending runtime |
| 10 | Settings interactions persist to `runtime-settings.json` | Pending runtime |
| 11 | Enable/disable toggles queue to `startup-commands.json` and apply next launch | Pending runtime |
| 12 | Content-only toggles hot-apply without writing a restart command | Pending runtime |
| 13 | `ErrorLog.log` remains empty for the validated run | Pending runtime |
| 14 | Achievement unlock attempts are blocked | Pending runtime |
| 15 | Save files land in `CrabtyaData/IsolatedSave/` and do not mutate base save | Pending runtime |

Also pending runtime (per earlier Jobs):
- Job 05 conflict detection — hot-toggle conflict re-detection
- Job 06 invert scroll — toggle appears in Mods menu, persists to `runtime-settings.json`
- Job 08 enemy stat patches — `EnemyStatPatchApplicator: patch applied` in `LogOutput.log`
- Job 09 AssetBundle proof — user must supply Unity 6000.2.15f1 bundle file

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

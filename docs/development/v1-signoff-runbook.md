# Crabtya v1 Sign-off Runbook

Use this runbook for the final full Crabtya `v1.0.0` validation pass.

## Test fixtures ready

- Valid DLL-only mod: `faniel.dll-sample`
- Valid hybrid mod: `crabtya.bundle-test`
- Missing DLL isolation fixture: `test.dll-missing`
- Throwing DLL isolation fixture: `test.dll-throws`

## Before launch

1. Build the loader:
   - `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`
2. Build the throwing DLL fixture if needed:
   - `dotnet build game/Mods/test.dll-throws/src/test.dll-throws.csproj -c Release`
3. Confirm the following logs are absent or freshly cleared:
   - `game/BepInEx/LogOutput.log`
   - `game/BepInEx/ErrorLog.log`
4. Confirm no old restart queue is present:
   - `game/Mods/startup-commands.json`

## Launch 1 - validate UI and queueing

1. Start the game through:
   - `game\__LAUNCHER.bat`
   - or `game\Everything is Crab.exe -timestamps`
2. In the main menu, verify:
   - the small `Crabtya` version label is visible
   - the `MODS` button is visible
   - clicking `MODS` opens the dedicated Mods settings menu
3. In Mods settings, enable these restart-required mods:
   - `faniel.dll-sample`
   - `crabtya.bundle-test`
   - `test.dll-missing`
   - `test.dll-throws`
4. Also verify one content-only settings path while you are there:
   - open `faniel.mousewheel-zoom`
   - confirm `Invert Scroll` is present
   - toggle it once and confirm it persists in `game/Mods/runtime-settings.json`
5. Verify restart-required queue behavior:
   - after toggling the four DLL/bundle fixtures, confirm `game/Mods/startup-commands.json` exists
   - confirm it contains queued enable commands for the mods you changed
6. Enter a run and verify:
   - pause/start menu contains `MOD SETTINGS`
   - clicking it opens the same dedicated Mods settings menu
7. Exit the game completely.

## After Launch 1

Check `game/BepInEx/LogOutput.log` for:

- `Plugin binary fingerprint`
- main menu and Mods menu startup lines as expected
- content-only mod application lines

Do not expect DLL/hybrid success yet if the restart-required queue was only created during this launch.

## Launch 2 - validate DLL, hybrid, and isolation results

1. Launch the game again with the same launcher path.
2. After reaching the main menu, close the game once startup finishes, or continue into the menus if you also want to reconfirm UI.

Check `game/BepInEx/LogOutput.log` for all of the following:

- Valid DLL-only load:
  - `faniel.dll-sample`
  - `Sample DLL mod OnLoad reached.`
- Valid hybrid load:
  - `AssetBundleApplicator: loaded bundle 'bundles/test-assets' for mod 'crabtya.bundle-test'.`
  - `BundleTest: bundle retrieved`
  - `BundleTest: PASS - Texture2D 'test_texture' loaded successfully.`
- Missing DLL isolation:
  - warning or error for `test.dll-missing`
  - other valid mods still continue loading
- Throwing DLL isolation:
  - `ThrowingEntrypoint: about to throw intentionally for isolation validation.`
  - exception or warning recorded for `test.dll-throws`
  - other valid mods still continue loading
- Queue consumption:
  - startup queue was processed
  - `game/Mods/startup-commands.json` is removed or emptied according to current behavior

Also confirm:

- `game/BepInEx/ErrorLog.log` is empty

## Additional required checks

1. Save isolation:
   - verify `game/CrabtyaData/IsolatedSave/` receives the run’s save writes
   - verify the base game save location is not mutated by the modded run
2. Achievement blocking:
   - confirm log lines for Steam achievement/stat blocking patches are present
3. Conflict hot-toggle:
   - enable `test.conflict-a` and `test.conflict-b`
   - confirm notices appear or clear live in the Mods UI without restart
4. Enemy stat patching:
   - enable `test.enemy-stat`, restart if needed, enter a run
   - confirm `EnemyStatPatchApplicator: patch applied` lines in `LogOutput.log`

## If Job 09 failure paths are being closed in the same pass

1. Missing bundle path:
   - enable `crabtya.bundle-missing` (or temporarily rename `game/Mods/crabtya.bundle-test/bundles/test-assets`)
   - launch once and confirm the `Referenced assetBundle file is missing` / `bundle file not found` warning
2. Corrupt bundle path:
   - enable `crabtya.bundle-corrupt` (or temporarily replace a bundle with a text file)
   - launch once and confirm `LoadFromFile returned null` warning while startup continues

## When the run passes

Update:

- `docs/development/jobs/job-10-v1-validation-release-packaging.md`
- `docs/development/jobs/job-09-assetbundle-proof.md` if AssetBundle proof is complete
- `docs/development/agent-handoff.md`
- `PLAN.md`

Then create the main full Crabtya `v1.0.0` release with:

- loader zip
- four user-facing mod zips
- optional sample/template artifacts
- install, uninstall, safe-mode, and verification notes

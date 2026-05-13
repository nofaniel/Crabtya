# Loader Compatibility

## v1 Decision Rule

- Default loader target: `BepInEx IL2CPP x64`.
- This remains default only if smoke test passes on this game build.
- If smoke test fails, stop BepInEx-specific implementation, document failure evidence, and pivot architecture to MelonLoader.

## Smoke Test Success Criteria

- Game launches through normal executable path.
- Loader bootstrap logs are generated.
- IL2CPP interop output is generated.
- A minimal startup plugin logs on game start.

## Smoke Test Failure Criteria

- Loader does not initialize.
- Expected logs or interop artifacts are missing.
- Game fails to boot reliably with loader installed.

## Current State

- Smoke test executed on 2026-05-10.
- Compatibility verdict: `PASS` for BepInEx IL2CPP on this build.

## Smoke Test Execution (2026-05-10)

- Loader build/version used: `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a`.
- Install path: `N:\Games\Everything.is.Crab\game`.
- Startup command/path used: `N:\Games\Everything.is.Crab\game\Everything is Crab.exe -timestamps`.
- Generated log/artifact paths:
  - `N:\Games\Everything.is.Crab\game\BepInEx\LogOutput.log`
  - `N:\Games\Everything.is.Crab\game\BepInEx\ErrorLog.log`
  - `N:\Games\Everything.is.Crab\game\BepInEx\interop\Assembly-CSharp.dll`
  - `N:\Games\Everything.is.Crab\game\BepInEx\interop\MethodAddressToToken.db`

## Criteria Results

- Game launches through normal executable path: `PASS`.
- Loader bootstrap logs are generated: `PASS`.
- IL2CPP interop output is generated: `PASS`.
- Minimal startup plugin logs on game start: `PASS` (`EIC Smoke Test Plugin loaded.`).

## Notes

- `ErrorLog.log` remained empty during smoke test runs.
- No fallback pivot to MelonLoader is required at this milestone.

## Required Evidence to Record

- Loader build/version used and install path.
- Exact startup command/path used.
- Generated log file paths.
- Pass/fail result for each success criterion.
- If failure: concise blocker summary and fallback decision note.

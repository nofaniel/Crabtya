# Job 03 - Full Code Review of Loader, Menus, and Mods

Source TODO paragraph: "Need full review of entire codebase of mod loader, mod menus, and mods - for any issues, bugs, poor code, issues on performance, polish."

## Objective

Perform a review pass across Crabtya loader runtime, native-style menu integrations, public API, packaged sample mods, and docs to find bugs, regressions, performance risks, polish issues, and missing verification. This Job should produce fixes where safe and a prioritized findings list where deeper work is needed.

## Scope

Review `loader/EIC.ModLoader/`, `loader/Crabtya.ModApi/`, `loader/SampleDllMod/`, `game/Mods/`, `templates/`, `tools/`, `docs/development/architecture.md`, and `docs/development/test-checklist.md`. Include menu UX paths: main-menu badge, main-menu `MODS`, pause-menu `MOD SETTINGS`, and `CrabtyaModsSettingsWindow`.

## Implementation Plan

1. Start with static review of startup flow, discovery, manifest validation, path safety, dependency resolution, content application, DLL entrypoint isolation, settings persistence, session isolation, and runtime hooks.
2. Review menu code for duplicate UI creation, leaked GameObjects, layout drift, unhandled nulls, repeated expensive scans, Input System exceptions, and failure handling.
3. Review per-frame/per-tick applicators for avoidable allocations, broad scene scans, idempotency problems, stale references, and failure isolation.
4. Review sample mods and templates for manifest correctness, clear README guidance, safe defaults, and consistency with `docs/mod-makers/manifest-format.md`.
5. Build the loader and capture compile warnings/errors.
6. Run focused runtime QA if code changes are made or if review findings require proof: startup, Mods menu open/close, toggles, settings persistence, DLL sample, and ErrorLog check.
7. Fix low-risk defects immediately. For larger or risky issues, create a prioritized findings section in `docs/development/agent-handoff.md` or a dedicated review report.
8. Update `docs/development/test-checklist.md` to distinguish verified fixes from still-pending review risks.

## Deliverables

- Code fixes for safe, well-bounded issues found during review.
- A prioritized review report with file/line references for unresolved issues.
- Updated test checklist and handoff notes.

## Verification

- `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` after any loader/API/template build changes.
- Runtime logs checked after any behavioral changes: `LogOutput.log` includes plugin fingerprint and expected startup lines; `ErrorLog.log` remains empty.
- For menu changes, manually open main menu Mods window and pause-menu Mod Settings window.
- For mod/template changes, verify the affected mod loads or fails in isolation as intended.

## Completion Criteria

- The review identifies concrete risks instead of vague cleanup wishes.
- Any fixed issues have build/runtime evidence.
- Remaining issues are prioritized enough that the next contributor can pick them up without re-reviewing everything.

## Status Update (2026-05-13)

- Status: Complete.
- Static/code hardening fixes landed this pass:
  - deterministic discovery + dependency ordering for stable startup/load order across runs;
  - DLL entrypoint type resolution narrowed to mod-local assemblies (prevents cross-mod type binding from unrelated loaded assemblies);
  - content warning dedupe during repeated hot-toggle re-apply (prevents warning list bloat);
  - AssetBundle key normalization for slash-style tolerant retrieval.
  - camera applicator scene-scan caching (periodic camera cache refresh + light pruning between scans) to avoid per-frame full `FindObjectsOfType<Camera>()`.
  - removed duplicate camera apply call from the 1-second runtime overlay tick (camera apply remains on `LateUpdate` + hook-triggered paths).
  - uiScale eligibility-root caching with transition-aware rescan cadence to reduce repeated global UI-type scans while preserving responsive transition behavior.
  - throttled high-frequency uiScale root-binding info logs during rapid transition churn to reduce log I/O noise.
  - cache pruning now excludes inactive cameras/UI roots between scan windows to keep cached apply targets fresh during menu/gameplay transitions.
  - camera patch selection now uses a per-frame supported-patch cache to avoid repeated filter/materialization work across camera/hook apply paths.
- Build evidence: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` passed with `0` warnings and `0` errors.
- Remaining review focus:
  - none for Job 03 closure; follow-up performance observations can be tracked as new scoped tasks if they appear during later Jobs.

## Closure Note (2026-05-13)

- Runtime follow-up completed with user validation: minor stutter at full zoom-out was rechecked and improved after camera-path perf hardening.
- Job 03 sign-off approved by user.

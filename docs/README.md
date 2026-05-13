# Everything is Crab Mod Loader Docs

This `docs/` tree is the source of truth for the loader effort.

## Sections

- `docs/investigation/`: confirmed game/runtime facts, compatibility notes, and hook research.
- `docs/development/`: implementation architecture, checklists, hook map, Job plans, and agent handoff state.
- `docs/users/`: install, disable, troubleshoot, and uninstall guidance for end users.
- `docs/mod-makers/`: manifest format, content workflow, packaging, and sample mod guidance.

## Current Milestone

- Milestone 2 complete: BepInEx IL2CPP smoke test passed on Unity `6000.2.15f1`.
- Current milestone: Crabtya v1 implementation hardening.
- Post-v1 planning now includes Crabtya Lite: a separate curated QoL-only package, not a user-mod loader, tracked as Job 11.
- Loader direction is Crabtya: folder mods in `game/Mods/<mod-id>/`, `eicmod.json` manifests, declarative + DLL mod support, and no loader-owned runtime mod menu.
- Runtime UI direction is now native-first: minimal main-menu Crabtya loaded/version label, native-styled main-menu `Mods` entry, in-run `MOD SETTINGS` entry, and the dedicated Crabtya Mods settings window as the single mod settings surface.
- Current applied/stable definition scope is `localization`, `balancePatch`, `cameraPatch`, `visual`, `introSkip`, and `uiScale`; `evolution`/`enemy` are recognized future surfaces and not applied yet.
- DLL mod contract now includes `assemblies` + `entrypoints` and the public `Crabtya.ModApi.dll` surface (`ICrabtyaMod`, `ICrabtyaModContext`, logging, settings registry).
- Runtime verification still relies on `game/BepInEx/LogOutput.log`, `game/BepInEx/ErrorLog.log`, and `game/Mods/mod-state.json`.
- Shareable package outputs are now produced by `tools/package-release.ps1` into `packages/loader/` and `packages/mods/`.

## Quick Links

- Investigation
  - `docs/investigation/build-facts.md`
  - `docs/investigation/discovered-symbols.md`
  - `docs/investigation/addressables-notes.md`
  - `docs/investigation/loader-compatibility.md`
- Development
  - `docs/development/jobs/README.md`
  - `docs/development/jobs/job-01-codebase-separation.md`
  - `docs/development/jobs/job-02-git-initial-readiness.md`
  - `docs/development/jobs/job-03-full-code-review.md`
  - `docs/development/jobs/job-04-loader-branding.md`
  - `docs/development/jobs/job-05-mod-api-conflicts.md`
  - `docs/development/jobs/job-06-scroll-zoom-invert-setting.md`
  - `docs/development/jobs/job-07-ui-scale-pop-polish.md`
  - `docs/development/jobs/job-08-enemy-stat-patching.md`
  - `docs/development/jobs/job-09-assetbundle-proof.md`
  - `docs/development/jobs/job-10-v1-validation-release-packaging.md`
  - `docs/development/jobs/job-11-crabtya-lite.md`
  - `docs/development/architecture.md`
  - `docs/development/test-checklist.md`
  - `docs/development/hook-map.md`
  - `docs/development/menu-extension-plan.md`
  - `docs/development/agent-handoff.md`
- Users
  - `docs/users/install-uninstall.md`
  - `docs/users/safe-mode.md`
  - `docs/users/troubleshooting.md`
- Mod Makers
  - `docs/mod-makers/manifest-format.md`
  - `docs/mod-makers/quickstart.md`
  - `docs/mod-makers/asset-workflow.md`
  - `docs/mod-makers/packaging.md`
  - `docs/mod-makers/sample-content-slice.md`

## Tooling

- Release packaging: `tools/package-release.ps1`
- Generated package output folder: `packages/README.md`

## Sample Mod READMEs

- `game/Mods/faniel.fov-slider/README.md`
- `game/Mods/faniel.mousewheel-zoom/README.md`
- `game/Mods/faniel.no-intro/README.md`
- `game/Mods/faniel.mvp-sample/README.md`
- `game/Mods/faniel.ui-scale/README.md`
- `game/Mods/persist.test/README.md`

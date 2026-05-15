# Crabtya Job Plans

This folder contains implementation Jobs expanded from earlier `TODO.md` ideas (Jobs 01-07), previously-untracked `PLAN.md` follow-ups (Jobs 08-10), and the current Crabtya Lite TODO note (Job 11).

`PLAN.md` is the master working document and Job board. Use this folder for the detailed per-Job implementation plans after selecting the active Job from `PLAN.md`.

Work one primary Job at a time unless `PLAN.md` marks a parallel lane as safe and write scopes stay disjoint. Each Job should end with code/docs updates, runtime or static verification evidence, and a short handoff note.

## Current Status Snapshot (2026-05-14)

- Jobs `01-10`: **Done** (v1 release-gate complete).
- Job `11` (`job-11-crabtya-lite.md`): **In progress** (architecture decision + separate Lite plugin scaffold + core Lite settings/zoom/invert/FOV systems implemented; runtime/package/docs closure pending).
- Active primary work should now focus on Job 11 only, unless the user explicitly requests maintenance work on completed Jobs.

## Recommended Execution Order

The file numbers are stable Job IDs, while the order below is the recommended work order from `PLAN.md`, balancing implementation ease, importance, dependency risk, and release readiness.

1. `job-01-codebase-separation.md` - foundation audit before more polish or publication work.
2. `job-06-scroll-zoom-invert-setting.md` - quick, high-value user-facing setting improvement.
3. `job-07-ui-scale-pop-polish.md` - important UI trust/polish pass for a public sample mod.
4. `job-03-full-code-review.md` - broad review after focused polish so it inspects the latest code.
5. `job-09-assetbundle-proof.md` - end-to-end verification of the existing AssetBundle pipeline with a real Unity 6000.2.15f1 bundle.
6. `job-04-loader-branding.md` - visible UX polish after correctness/menu review unless final art is ready earlier.
7. `job-05-mod-api-conflicts.md` - larger API/loader evolution after the surface is cleaner.
8. `job-08-enemy-stat-patching.md` - add the `enemy.stat.*` declarative balance-patch surface after a runtime probe pass.
9. `job-10-v1-validation-release-packaging.md` - full v1 Validation Checklist + release packaging gate.
10. `job-02-git-initial-readiness.md` - final GitHub/publication polish after previous v1 Jobs leave the repo in good shape.
11. `job-11-crabtya-lite.md` - post-v1 Crabtya Lite product split for curated QoL-only native settings without full-loader mod surfaces.

Parallelism guidance lives in `PLAN.md`. Default to one primary Job at a time; only parallelize disjoint write scopes.

## Shared Completion Rules

- Do not modify original game binaries or bundled assets under `game/Everything is Crab_Data/`, the base executable, or base DLLs.
- Prefer changes in `loader/`, `game/Mods/<mod-id>/`, `templates/`, `tools/`, `docs/`, and `packages/` as appropriate for the active Job.
- Build with `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` whenever loader or API code changes.
- Use runtime evidence from `game/BepInEx/LogOutput.log`, `game/BepInEx/ErrorLog.log`, `game/Mods/mod-state.json`, `game/Mods/startup-commands.json`, and `game/Mods/runtime-settings.json` whenever behavior changes.
- Update `docs/development/agent-handoff.md` after every meaningful milestone.

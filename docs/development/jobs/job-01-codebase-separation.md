# Job 01 - Verify Loader/Mod Codebase Separation

Source TODO paragraph: "Check the mod loader codebase is entirely seperate from currently developed mods - need to ensure no crossover occured during development of mods."

## Objective

Prove that Crabtya loader/API code, sample mod projects, installed mod folders, packaged mod outputs, and templates have clean ownership boundaries. Development of bundled/sample mods must not have leaked mod-specific behavior into the core loader, except for deliberate generic extension points documented as v1 surfaces.

## Scope

Review `loader/EIC.ModLoader/`, `loader/Crabtya.ModApi/`, `loader/SampleDllMod/`, `game/Mods/`, `templates/`, `tools/package-release.ps1`, and package outputs under `packages/`. This Job is mostly audit/refactor work; do not add new loader features unless a separation bug requires it.

## Implementation Plan

1. Inventory project ownership by listing every `.csproj`, source file, manifest, template, and package script input/output path.
2. Search loader code for sample or personal mod identifiers such as `faniel.`, `persist.test`, `test.visual-targets`, specific sample asset names, and sample-only setting IDs.
3. Confirm all mod-specific behavior is represented as data loaded from `eicmod.json` and definition files, not hard-coded into generic applicators.
4. Confirm `loader/Crabtya.ModApi/` contains only public, game-agnostic API contracts and no dependency on installed sample mods.
5. Confirm `loader/SampleDllMod/` is treated as a sample/template input, not as a runtime dependency for Crabtya itself.
6. Inspect packaging scripts to ensure loader packages include only loader-owned files, API assemblies, scripts, docs, and templates, while mod zip packages are generated separately.
7. If crossover exists, refactor it behind manifest fields, content definitions, settings metadata, or sample mod data as appropriate.
8. Document the final ownership boundaries in `docs/development/architecture.md` and update `docs/development/agent-handoff.md` with findings.

## Deliverables

- A short separation report added to `docs/development/agent-handoff.md` or a dedicated `docs/development/codebase-separation-audit.md` if the findings are non-trivial.
- Any required refactors that remove hard-coded sample-mod behavior from loader code.
- Updated docs if ownership rules or package contents change.

## Verification

- `rg -n "faniel\.|persist\.test|test\.visual-targets|SampleDll" loader/EIC.ModLoader loader/Crabtya.ModApi` should show no improper core-loader coupling. References in logs, comments, templates, package scripts, or intentional sample locations must be explained.
- `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release` if any loader/API code changes.
- Package script dry run or full run if package boundaries change.
- `game/BepInEx/ErrorLog.log` remains empty after runtime verification if code changes are made.

## Completion Criteria

- Future contributors can tell where loader code ends and mod code begins.
- Bundled mods can be removed without breaking Crabtya bootstrap, discovery, menu rendering, or public API loading.
- Loader packages and mod packages are cleanly separated.

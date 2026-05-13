# Job 08 - Enemy Stat Patching Surface — Done 2026-05-13

Source PLAN follow-up: "Enemy stat patching surface (`enemy.stat.*` balance patches) - class names need one runtime probe pass to confirm."

## Status

**Done (2026-05-13) — runtime verified.** Hook confirmed, applicator implemented, test fixtures created, docs updated, and runtime evidence captured.

Runtime log evidence:
- `EnemyStatPatchApplicator: patch 'test.enemy-stat.global-maxhp' applied (archetype=all). Member=MaxHp.FlatValue, Op=multiply, Patch=0.5, Before=18, After=9` (multiple enemies)
- `Applied balance patch for mod 'test.enemy-stat': Id=test.enemy-stat.global-maxhp, Target=enemy.stat.MaxHp, Op=multiply, Value=0.5`
- `Applied balance patch for mod 'test.enemy-stat': Id=test.enemy-stat.blobfish-speed, Target=enemy.BlobFish.stat.MovementSpeed, Op=multiply, Value=2.0`

Note: `test.enemy-stat/eicmod.json` was missing `targetGame` and `loaderVersion` fields (mod was silently skipped at startup). Fixed during this validation pass.

## Confirmed Hook (from interop DLL inspection 2026-05-13)

- **Hook type**: `World.Characters.Enemies.BasicEnemyCharacter`
- **Hook method**: `InitEnemyInstanceStatsIfNeeded` (called once per enemy instance on init)
- **Stats class**: `World.Stats.EnemyStats` — resolved from `BasicEnemyCharacter.EntityStats`
- **Archetype**: `EEnemyArchetype` enum — resolved from `BasicEnemyCharacter.EnemyArchetype`
- **Stats inheritance**: `EnemyStats → AEnemyStats → AEntityStats → Il2CppSystem.Object`

`ResetStatsToDefaultState` (used for PlayerStats) does **not** exist on EnemyStats or any of its base classes in the interop DLL. `InitEnemyInstanceStatsIfNeeded` is the correct enemy-side equivalent.

## Confirmed EnemyStats Properties

`AttackAreaModifier`, `BaseAbilityDamage`, `BasePhysicalDamage`, `CataclysmReductionPercentage`,
`CharmResistance`, `ChaseSpeed`, `ColdAdaptation`, `CooldownModifier`, `FeedingDistance`,
`FeedingSpeed`, `FleeSpeed`, `GeneralDamageMultiplier`, `HeatAdaptation`, `HpRegeneration`,
`MaxHp`, `MaxHpIncreaseFromHpFood`, `MaxPlating`, `MovementSpeed`, `NumExtraPoisonTics`,
`PoisonDamageMultiplier`, `PoisonReductionPercentage`, `PoisonTicksSpeedModifier`,
`SandTerrainAdaptation`, `ShieldedStatusDamageResistance`, `Size`, `SnowTerrainAdaptation`,
`SoakAdaptation`, `SprintRecoveryImpactSpeedMultiplier`, `SprintSpeedMultiplier`,
`StunDurationMultiplier`, `StunReductionPercentage`, `TerrainAdaptation`, `TurningSpeed`,
`WaterTerrainAdaptation`.

## Target Format

- `enemy.stat.<statName>` — applies to all enemies
- `enemy.<EEnemyArchetype>.stat.<statName>` — applies only to that archetype

## Test Fixtures

`game/Mods/test.enemy-stat/` (defaultEnabled: false) — two patches:
- `test.enemy-stat.global-maxhp`: `enemy.stat.MaxHp multiply 0.5` (all enemies)
- `test.enemy-stat.blobfish-speed`: `enemy.BlobFish.stat.MovementSpeed multiply 2.0` (BlobFish only)

## Implementation Notes

- `loader/EIC.ModLoader/EnemyStatPatchApplicator.cs` — new file, mirrors `BalancePatchApplicator.cs`
- `Plugin.cs` — `EnemyStatPatchApplicator.Install(Log)` added after `BalancePatchApplicator.Install(Log)`
- No changes to `ContentPipeline.cs` or `ContentDefinitions.cs` — `balancePatch` type already routes both player and enemy patches
- Build: 0 errors, 0 warnings (confirmed 2026-05-13)



## Objective

Add a generic `enemy.stat.*` declarative balance-patch surface that mirrors the existing `player.stat.*` pipeline, so mods can rebalance enemy values without DLL code. The surface must be data-driven, isolated per mod, and apply through a confirmed runtime hook on the enemy stats class rather than guesswork.

## Scope

Work in `loader/EIC.ModLoader/BalancePatchApplicator.cs` (or a sibling `EnemyStatPatchApplicator.cs` if a second hook target is cleaner), `loader/EIC.ModLoader/ContentDefinitions.cs`, `loader/EIC.ModLoader/ContentPipeline.cs`, and manifest validation. Update `docs/mod-makers/manifest-format.md`, the balance-patch examples in `templates/content-mod-template/`, and any user/dev docs that currently say enemy stats are future-facing. Do not touch original game binaries; this is a Harmony-patched runtime hook.

## Implementation Plan

1. Runtime probe pass: enable a temporary diagnostic that logs candidate enemy stats type names (e.g. `World.Stats.EnemyStats`, `EnemyStats`, or a per-enemy stats container) and the methods that look analogous to `PlayerStats.ResetStatsToDefaultState` / `MarkStatsWhichCanBeReceiveStatChangeMultiplierEffect` / `EnableEnforceRangeLimits`. Capture findings in `game/BepInEx/LogOutput.log` and write the confirmed type/method names to this Job plan and to `docs/development/architecture.md` before writing the production hook.
2. Decide whether enemy stats live on a single shared class or per-enemy instances. If per-instance, scope the hook to the enemy-spawn/init pathway and apply patches keyed by enemy id/archetype.
3. Extend `ContentDefinitions` so manifests can declare `enemy.stat.<statName>` (and optionally `enemy.<enemyId>.stat.<statName>` if per-enemy scoping is required) with the same operator vocabulary already used for `player.stat.*` (set/add/mul/clamp).
4. Implement application via Harmony postfix on the confirmed hook. Mirror the failure-isolation behavior of `BalancePatchApplicator` so one bad mod does not block other valid mods.
5. Add manifest validation: unknown stat names, invalid operators, or escapes outside `enemy.stat.*` should produce a clear `LogOutput.log` warning and skip just that patch.
6. Add an opt-in sample/test fixture (under `game/Mods/` with a clearly non-release id, or in a dev-only fixtures folder) that proves an enemy stat moves at runtime, then remove or gate it before release.
7. Update `docs/mod-makers/manifest-format.md`, the content-mod template README, and any "future-facing" notes in `PLAN.md` / `docs/development/architecture.md` to move `enemy` from discovery-only to active for the stat surface.

## Deliverables

- Confirmed enemy stats type/method names recorded in `docs/development/architecture.md`.
- `enemy.stat.*` balance patches applied at runtime through an isolated Harmony hook.
- Manifest schema, validation, and mod-maker docs updated.
- Runtime proof for at least one enemy stat change captured in `LogOutput.log`.

## Verification

- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- Runtime: load a test mod that changes a single enemy stat; observe the value reflected during gameplay and a clear apply line in `LogOutput.log`.
- Runtime: a manifest with an invalid `enemy.stat.<name>` entry is skipped with a warning while the rest of the mod still loads.
- Runtime: with no enemy stat patches enabled, behavior is unchanged.
- `game/BepInEx/ErrorLog.log` remains empty for the validated run.

## Completion Criteria

- Mod makers can rebalance enemy stats declaratively without DLL code.
- The hook is documented well enough that another agent can extend or replace it without re-probing.
- `PLAN.md` no longer lists enemy stat patching as a planned follow-up outside the Job board.

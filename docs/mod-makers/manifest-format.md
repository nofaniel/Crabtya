# Manifest Format (`eicmod.json`)

Each mod lives in `Mods/<mod-id>/` and must include `eicmod.json`.

## Required Fields

- `id`: globally unique mod ID (recommended: reverse-domain style).
- `name`: display name.
- `version`: semantic version string.
- `author`: author or team name.
- `description`: short summary.
- `targetGame`: expected `Everything is Crab`.
- `targetUnity`: expected Unity version (current docs target `6000.2.15f1`).
- `loaderVersion`: compatible loader API/version range.
- `content`: content declaration object.
- `defaultEnabled`: default enabled state for first discovery.

## Optional Fields

- `dependencies`: list of mod IDs that must be enabled and loaded before this mod.
- `conflicts`: list of mod IDs that cannot be active alongside this mod. When Crabtya detects that two enabled mods declare a mutual conflict, the mod that appears later in load order is blocked for that launch. Both mods receive notices in the dedicated Mods settings menu. Example:

```json
"conflicts": ["other.mod-id"]
```

Declaration is one-directional: you only need to declare the conflict in your manifest. If the other mod also declares it, Crabtya deduplicates the pair and blocks exactly one.

### DLL Mod Contract (v1)

For managed DLL mods, both of these fields are required together:

- `assemblies`: list of relative DLL paths under the mod folder.
- `entrypoints`: list of fully qualified type names implementing `Crabtya.ModApi.ICrabtyaMod`.

If a mod declares only one of these fields, loader validation marks the mod as errored.

## Baseline Template

```json
{
  "id": "author.mod-id",
  "name": "Display Name",
  "version": "1.0.0",
  "author": "Author",
  "description": "Short description",
  "targetGame": "Everything is Crab",
  "targetUnity": "6000.2.15f1",
  "loaderVersion": "1.x",
  "dependencies": [],
  "content": {
    "catalogs": [],
    "assetBundles": [],
    "definitions": []
  },
  "assemblies": [],
  "entrypoints": [],
  "defaultEnabled": true
}
```

## DLL-Only Template

```json
{
  "id": "author.dll-mod",
  "name": "DLL Mod",
  "version": "1.0.0",
  "author": "Author",
  "description": "Sample DLL entrypoint mod",
  "targetGame": "Everything is Crab",
  "targetUnity": "6000.2.15f1",
  "loaderVersion": "1.x",
  "dependencies": [],
  "content": {
    "catalogs": [],
    "assetBundles": [],
    "definitions": []
  },
  "assemblies": [
    "bin/author.dll-mod.dll"
  ],
  "entrypoints": [
    "Author.DllMod.Entrypoint"
  ],
  "defaultEnabled": true
}
```

## Definition Type Status

Applied and supported in v1:

- `localization`
- `balancePatch`
- `cameraPatch`
- `visual`
- `introSkip`
- `uiScale`
- `content.assetBundles` — Unity AssetBundle files loaded at startup before DLL entrypoints; restart-required when toggled. See [Asset Workflow](asset-workflow.md) for the Unity build recipe.
- `assemblies` + `entrypoints` — supported for v1 DLL mods through `Crabtya.ModApi`. Broad direct game patching is advanced and unsupported.

Recognized/discovered for future content work, not applied yet:

- `evolution`

Possible but not stable:

- `content.catalogs` paths are validated if declared and should be treated as restart-sensitive; catalog unload is not promised.

Any other `type` value is treated as unsupported and skipped with a warning. This means experiments can be discovered without breaking the whole mod, but only the applied definitions above have runtime effect today.

## MVP Definition Examples

`balancePatch` targets `player.stat.*` for player stats and `enemy.stat.*` / `enemy.<archetype>.stat.*` for enemy stats. The operation vocabulary (`set`, `add`, `multiply`) is the same for both.

**Player stat patch** — applies once when the player's stats refresh:
```json
{
  "type": "balancePatch",
  "id": "author.mod.player-speed",
  "target": "player.stat.MovementSpeed",
  "op": "multiply",
  "value": 1.2
}
```

**Global enemy stat patch** — applies to every enemy of every archetype when initialized:
```json
{
  "type": "balancePatch",
  "id": "author.mod.enemy-hp",
  "target": "enemy.stat.MaxHp",
  "op": "multiply",
  "value": 0.75
}
```

**Per-archetype enemy stat patch** — applies only to the named `EEnemyArchetype` value:
```json
{
  "type": "balancePatch",
  "id": "author.mod.blobfish-speed",
  "target": "enemy.BlobFish.stat.MovementSpeed",
  "op": "multiply",
  "value": 2.0
}
```

Confirmed `EEnemyArchetype` values (v1): `BlobFish`, `BlobFish_Mid`, `BlobFish_Late`, `Beeware`, `Beeware_Mid`, `Beeware_Late`, `SnowHare`, `SnowHare_Mid`, `SnowHare_Late`, `Turtoid`, `Turtoid_Mid`, `Turtoid_Late`, `HatBirb`, `HatBirb_Mid`, `HatBirb_Late`, `Pantther`, `Pantther_Mid`, `Pantther_Late`, `Mimic`, `Mimic_Mid`, `Mimic_Late`, `Thief`, `Thief_Mid`, `Thief_Late`, `Sandshark`, `Sandshark_Mid`, `Sandshark_Late`, `Spitfish`, `Spitfish_Mid`, `Spitfish_Late`, `Spiderfrog`, `Spiderfrog_Mid`, `Spiderfrog_Late`, `Crabbybara`, `Crabbybara_Mid`, `Boss_Crabtaur`, `Boss_Crabtaur_Mid`, `Boss_Crabtaur_Late`, `Boss_Aquaconda`, `Boss_Aquaconda_Mid`, `Boss_Aquaconda_Late`, `Boss_SpiderCrabette`, `Boss_SpiderCrabette_Mid`, `Boss_SpiderCrabette_Late`, `Boss_Shellephant`, `Boss_Shellephant_Mid`, `Boss_Shellephant_Late`, `Boss_Krabaroo`, `Boss_Krabaroo_Mid`, `Boss_Krabaroo_Late`, `FinalBoss_Krabken`, `Special_Spiderling`, `Special_Spiderling_Mid`, `Special_Spiderling_Late`, `Special_Krabken_Tentacle`, `Crabtaur_Baby`, `Aquaconda_Baby`, `SpiderCrabette_Baby`, `Shellephant_Baby`, `Krabaroo_Baby`.

Confirmed `EnemyStats` properties available for patching: `AttackAreaModifier`, `BaseAbilityDamage`, `BasePhysicalDamage`, `CataclysmReductionPercentage`, `CharmResistance`, `ChaseSpeed`, `ColdAdaptation`, `CooldownModifier`, `FeedingDistance`, `FeedingSpeed`, `FleeSpeed`, `GeneralDamageMultiplier`, `HeatAdaptation`, `HpRegeneration`, `MaxHp`, `MaxHpIncreaseFromHpFood`, `MaxPlating`, `MovementSpeed`, `NumExtraPoisonTics`, `PoisonDamageMultiplier`, `PoisonReductionPercentage`, `PoisonTicksSpeedModifier`, `SandTerrainAdaptation`, `ShieldedStatusDamageResistance`, `Size`, `SnowTerrainAdaptation`, `SoakAdaptation`, `SprintRecoveryImpactSpeedMultiplier`, `SprintSpeedMultiplier`, `StunDurationMultiplier`, `StunReductionPercentage`, `TerrainAdaptation`, `TurningSpeed`, `WaterTerrainAdaptation`.

`cameraPatch` currently supports only `camera.orthographicSize`:

```json
{
  "type": "cameraPatch",
  "id": "author.mod.camera",
  "target": "camera.orthographicSize",
  "op": "multiply",
  "value": 1.35
}
```

To expose a native settings slider row for a camera patch, add a `settings.slider` block. The loader persists changed values in `Mods/runtime-settings.json` and uses the adjusted value in place of the definition's default `value`.

```json
{
  "type": "cameraPatch",
  "id": "author.mod.gameplay-fov",
  "target": "camera.orthographicSize",
  "op": "multiply",
  "value": 1.0,
  "settings": {
    "slider": true,
    "label": "FOV",
    "min": 0.75,
    "max": 1.75,
    "step": 0.05
  }
}
```

To expose mouse-wheel live zoom for a camera patch, set `settings.mouseWheel` to `true`. This keeps the mod separate from slider-driven mods while reusing the same `camera.orthographicSize` patch pipeline.

```json
{
  "type": "cameraPatch",
  "id": "author.mod.mousewheel-zoom",
  "target": "camera.orthographicSize",
  "op": "multiply",
  "value": 1.0,
  "settings": {
    "mouseWheel": true,
    "invert": false,
    "label": "Wheel Zoom",
    "min": 0.75,
    "max": 2.0,
    "step": 0.05
  }
}
```

`settings.invert` (default `false`) sets the initial scroll direction for any `mouseWheel: true` camera patch. Crabtya also renders an `Invert Scroll` toggle next to the patch in the dedicated Mods settings menu, persisted as a bool under `<patch.id>.invert` in `game/Mods/runtime-settings.json`. The persisted user value overrides the manifest default at runtime; absent any persisted value, the manifest default is used.

This now attempts to add a row in the game's native settings UI via Crabtya-owned injection.
If native row injection cannot be resolved on the current game build, Crabtya logs a warning and continues loading mods.

`visual` supports multiple target surfaces.

**`player.baseSprite`** — replaces sprite renderers on the player character:
```json
{
  "type": "visual",
  "id": "author.mod.player-sprite",
  "target": "player.baseSprite",
  "image": "assets/base-crab.png",
  "pixelsPerUnit": 100
}
```

**`enemy.baseSprite`** — replaces sprites on any live component whose type name contains `Enemy`, `Creature`, `Boss`, or `Npc` (case-insensitive). Applies to newly-discovered objects on the ~1 s tick:
```json
{
  "type": "visual",
  "id": "author.mod.enemy-sprite",
  "target": "enemy.baseSprite",
  "image": "assets/enemy-custom.png",
  "pixelsPerUnit": 100
}
```

**`gameobject.named:<pattern>`** — replaces sprites on any active GameObject whose name *contains* the given pattern (case-insensitive). Useful for targeting specific prefab instances by their Unity editor name:
```json
{
  "type": "visual",
  "id": "author.mod.boss-sprite",
  "target": "gameobject.named:BigCrab",
  "image": "assets/big-crab.png",
  "pixelsPerUnit": 100
}
```

**`sprite.named:<pattern>`** — scans all live `SpriteRenderer`s and replaces those whose current sprite asset name *contains* the pattern (case-insensitive). Targets by sprite asset name rather than by object or type:
```json
{
  "type": "visual",
  "id": "author.mod.named-sprite",
  "target": "sprite.named:crab_idle",
  "image": "assets/crab-idle-custom.png",
  "pixelsPerUnit": 100
}
```

All four targets are hot-reloadable (camera/visual mods apply live without restart).

`introSkip` can skip intro/splash video playback by matching known substrings from the video source probe:

```json
{
  "type": "introSkip",
  "id": "author.mod.no-intro",
  "mode": "containsAny",
  "matchContains": [
    "intro",
    "opening",
    "splash",
    "logo"
  ]
}
```

`uiScale` is supported and allows users to uniformly scale native UI controls individually for accessibility. It preserves the game's layout transforms by scaling eligible controls from their captured baseline without promoting them into parent containers or rewriting pivots. Broad presentation UI such as level-select progression/reward art may shrink below native size, but Crabtya caps those surfaces at native size when the slider is above `1x` so max scale does not oversize already-large UI. Runtime refresh is adaptive (faster while non-`1x`, lighter while at `1x`) and adds a short transition burst (near-frame cadence) when menu roots change; the same fast cadence is held while the quit-confirmation popup (`GenericDecisionPopup`) is active to reduce first-frame pop. Crabtya's own dedicated Mods settings window remains excluded.

Crabtya-injected menu button clones (main-menu `MODS` and pause-menu `MOD SETTINGS`) now copy the template's captured baseline scale when available, so they match neighboring native button sizing under non-`1x` uiScale.

```json
{
  "type": "uiScale",
  "id": "author.mod.ui-scale",
  "target": "ui.scale",
  "op": "set",
  "value": 1.0,
  "settings": {
    "slider": true,
    "label": "UI Scale",
    "min": 0.5,
    "max": 1.15,
    "step": 0.05
  }
}
```

## AssetBundle Mods

Asset bundles are pre-built Unity binary files that ship custom assets (textures, audio clips, prefabs, materials, etc.) with your mod.

**Manifest declaration:**
```json
"content": {
  "assetBundles": ["bundles/my-assets"]
}
```

**Notes:**
- The bundle file must be built with Unity **6000.2.15f1** (the same version as the game). Bundles built with other Unity versions will fail to load.
- Bundle files typically have no extension — they are raw binary AssetBundle files.
- Loaded bundles are available to DLL mods through `ICrabtyaModContext.AssetBundles`.
- Mods with declared asset bundles are **restart-required** when toggled (bundles cannot be safely unloaded at runtime).
- All bundle paths must be relative to the mod folder and may not use `..`.

**DLL mod access example:**
```csharp
if (context.AssetBundles.TryGetBundle("bundles/my-assets", out _))
{
    var texture = context.AssetBundles.LoadAsset<Texture2D>(
        "bundles/my-assets",
        "assets/textures/my_texture.png");
}
```

Use `context.AssetBundles.LoadAsset<T>()` / `LoadAllAssets<T>()` for runtime extraction from
loaded bundles. These API calls route through Crabtya's Unity 6 compat layer and avoid the
direct `bundle.LoadAsset<T>()` / `bundle.LoadAllAssets()` wrapper issue on this stack.

## Validation Rules (v1)

- Duplicate `id` values are rejected.
- Missing required fields produce error state and skip load.
- Dependency order is resolved before apply.
- Target mismatch should warn and use safe behavior rather than risky apply.
- Discovered-only and unsupported definition types are skipped during apply, but they do not by themselves hard-fail the mod.

## Current Discovery Validation Behavior

- `id` must be reverse-domain style (for example: `author.mod-id`).
- `name`, `version`, `author`, `description`, `targetGame`, `loaderVersion`, and `content` are required.
- `targetGame` must match `Everything is Crab`.
- `targetUnity` mismatch currently warns; it does not hard-fail.
- `loaderVersion` outside `1.x` currently warns; it does not hard-fail.
- `defaultEnabled` is used as an initial preference. Once discovered, user state in `Mods/mod-state.json` is authoritative on later launches.
- `assemblies` paths are validated as files under the mod folder.
- `entrypoints` must be non-empty fully qualified type names.
- DLL mods must provide both `assemblies` and `entrypoints`.
- `content.catalogs`, `content.assetBundles`, and `content.definitions` paths must be relative, may not contain `..`, and must exist.
- `content.assetBundles` entries are loaded at startup before DLL entrypoints run (`LoadFromStream` with fallbacks). Bundles must be built with Unity 6000.2.15f1 for StandaloneWindows64. Mods declaring asset bundles are restart-required when toggled. DLL entrypoints access loaded bundles through `ICrabtyaModContext.AssetBundles` (`TryGetBundle`, `GetAssetNames`, `LoadAsset<T>`, `LoadAllAssets<T>`). See [Asset Workflow](asset-workflow.md) for the full build recipe.
- Missing dependencies mark a mod as errored.
- Errored mods are skipped and retained in place; they are not moved automatically.
- If a startup toggle changes a mod that declares `assemblies` and/or `content.catalogs`, loader state marks restart-required reasons in `Mods/mod-state.json`.
- `content.definitions` with `type` of `evolution` are currently logged as recognized future surfaces and remain discovery-only.

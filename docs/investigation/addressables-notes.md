# Addressables Notes

## Confirmed Local State

- Addressables settings file:
  - `game/Everything is Crab_Data/StreamingAssets/aa/settings.json`
- Catalog files present:
  - `game/Everything is Crab_Data/StreamingAssets/aa/catalog.bin`
  - `game/Everything is Crab_Data/StreamingAssets/aa/catalog.hash`
- Main content target: `StandaloneWindows64`
- Addressables version in settings: `2.7.6`

## Localization Signal

- Multiple localization bundles are present under:
  - `game/Everything is Crab_Data/StreamingAssets/aa/StandaloneWindows64/`
- Locale bundle names include English, French, German, Italian, Spanish, Japanese, Korean, Chinese variants, and others.

## Modding Implications

- Optional additional content catalogs should load via `Addressables.LoadContentCatalogAsync`.
- Loaded Addressables catalogs cannot be unloaded during runtime in a way that fully restores pre-load state.
- v1 behavior should therefore mark catalog-affecting changes as restart-required.

## v1 Safety Behavior

- Enabling/disabling mods that register new catalogs should update desired state immediately.
- Effective removal of already loaded catalog-provided assets should occur on next launch.
- UI and logs must clearly state when restart is required.

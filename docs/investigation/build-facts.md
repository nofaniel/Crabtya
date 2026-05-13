# Build Facts

## Confirmed Local Facts

- Game title: `Everything is Crab`
- Studio string: `Odd Dreams Digital`
- Platform target in plan: Windows x64
- Runtime model: IL2CPP (`GameAssembly.dll` present with `global-metadata.dat`)
- Unity player: `UnityPlayer.dll` present
- Unity build guid from `boot.config`: `fff6824e29e948e7b1c599102a801f94`

## Unity Version

- Plan target states Unity `6000.2.15f1`.
- This should be treated as the current expected version gate for v1 docs and mod manifests.

## Confirmed Integration Signals

- Addressables runtime settings file found at:
  - `game/Everything is Crab_Data/StreamingAssets/aa/settings.json`
- Addressables package version reported: `2.7.6`
- Wwise integration present:
  - `AkSoundEngine.dll`
  - `StreamingAssets/Audio/GeneratedSoundBanks/`
- EOS integration present:
  - `EOSBootstrapper.exe`
  - `EOSSDK-Win64-Shipping.dll`
  - `StreamingAssets/EOS/*.json`
- Steamworks present:
  - `Steamworks.NET Version: 2024.8.0`
  - `Steamworks SDK Version: 1.60`

## Operational Constraints

- Do not modify original game binaries or original bundled assets.
- Loader output, docs, SDK files, sample mods, and packaged artifacts are allowed additions.
- Use smoke-test-first loader validation before deeper runtime integration work.

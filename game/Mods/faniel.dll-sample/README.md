# Sample DLL Mod (`faniel.dll-sample`)

## What this mod does

- Demonstrates the Crabtya DLL entrypoint contract via `ICrabtyaMod`.
- Registers native settings (`bool`, `int`, `float`, and option) during `OnLoad`.
- Writes sample loader-scoped log lines through `ICrabtyaLogger`.

## Construction

Folder layout:

- `eicmod.json`: manifest and DLL entrypoint contract.
- `bin/faniel.dll-sample.dll`: built mod assembly.

Entrypoint contract in this sample:

- `assemblies`: `bin/faniel.dll-sample.dll`
- `entrypoints`: `Faniel.DllSample.SampleDllEntrypoint`

## Build + Deploy

1. Build the sample assembly:
   - `dotnet build loader/SampleDllMod/SampleDllMod.csproj -c Release`
2. Copy the built DLL into this mod folder:
   - from `loader/SampleDllMod/bin/Release/net6.0/faniel.dll-sample.dll`
   - to `game/Mods/faniel.dll-sample/bin/faniel.dll-sample.dll`
3. Enable `faniel.dll-sample` and launch the game.

## Verification

- Check `game/BepInEx/LogOutput.log` for:
  - `DLL mod 'faniel.dll-sample' entrypoint loaded: Faniel.DllSample.SampleDllEntrypoint`
  - `[CrabtyaMod:faniel.dll-sample] Sample DLL mod OnLoad reached.`
- Check `game/Mods/runtime-settings.json` for stored keys prefixed with `faniel.dll-sample:`.
- Open native settings and confirm the Crabtya section includes rows for this mod settings set.

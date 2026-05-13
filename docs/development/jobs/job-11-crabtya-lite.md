# Job 11 - Crabtya Lite QoL Loader

Source TODO paragraph: "Crabtya-lite: Once v1 of Crabtya is done, I want a lite version. Small easy basic - minimal interference version - mod loader which can be installed onto main install of game. Installed Zoom scroll option in native settings, adds fov option in native video settings. Does not run on isolated save or prevent achievements. Crabtya-lite will be to serve as QOL only mods - pre-installed into the lite loader not user installed."

## Objective

Design and build a separate post-v1 **Crabtya Lite** package: a minimal, curated quality-of-life loader for the main game install that exposes only pre-installed QoL settings through native-feeling settings UI. Lite is not a user mod loader and must not inherit full Crabtya's arbitrary mod loading surface.

## Scope

Start with a short architecture/product split spike, then implement only the smallest safe Lite package. Likely write areas are a new `loader/Crabtya.Lite/` project or an explicitly separated packaging path, shared QoL applicator code if it can be extracted safely, `tools/` package scripts, `packages/`, and Lite-specific user docs. Do not modify original game binaries or bundled assets under `game/Everything is Crab_Data/`.

Out of scope for Lite:

- `game/Mods/<mod-id>/` discovery.
- `eicmod.json` manifests.
- Declarative content surfaces.
- DLL entrypoint mods or `Crabtya.ModApi` loading.
- Dependency resolution, mod state, startup command queues, or hot-toggle mod state.
- Full Crabtya Mods settings window, runtime overlay/debug panel, or user-facing mod enable/disable controls.
- Save isolation and achievement blocking, unless Lite grows beyond curated QoL settings and must fall back to the full Crabtya safety model.

## Implementation Plan

1. Product decision gate: write down the exact Lite contract before coding. Lite may preserve achievements only because it runs curated QoL patches and never loads arbitrary user mods. If that premise changes, stop and redesign around full Crabtya's safety model.
2. Choose the architecture:
   - Preferred: a separate BepInEx plugin project such as `loader/Crabtya.Lite/` with only the Lite bootstrap and QoL applicators.
   - Acceptable: a shared internal library for reusable applicators plus separate full/Lite plugin projects.
   - Avoid: a runtime flag inside the full loader that could accidentally leave discovery, DLL loading, save isolation, achievement blocking, or Mods UI half-enabled.
3. Define the initial built-in feature set:
   - mouse-wheel zoom / scroll zoom;
   - invert scroll;
   - FOV option in the native video/settings surface.
4. Reuse existing proven code only after separating loader-owned concerns from feature-owned concerns. In particular, do not pull in `ModDiscovery`, `DllModLoader`, `LiveModRegistry`, `CrabtyaModsSettingsWindow`, `SessionIsolationGuard`, or manifest/state storage.
5. Add native-feeling settings integration for Lite-owned options. Prefer the game's existing settings/video menu surfaces; if native insertion fails, log clearly and keep gameplay safe.
6. Add Lite package scripts/artifacts separate from full Crabtya release artifacts. Naming should make the distinction obvious, e.g. `Crabtya-Lite-vX.Y.Z.zip` versus `Crabtya-vX.Y.Z.zip`.
7. Add user docs that explain the choice:
   - Use Crabtya Lite for curated QoL settings only.
   - Use full Crabtya for folder mods, content mods, DLL mods, mod settings, and safer arbitrary modded sessions.
   - Do not install both unless the compatibility story has been explicitly tested and documented.
8. Runtime-verify Lite from a clean install. Confirm the full loader is not present, no `Mods/` scanning occurs, no isolated save directory is used, achievements are not blocked by Lite code, and the native settings controls persist.
9. Update `PLAN.md`, `docs/development/architecture.md`, `docs/users/`, packaging docs, and `docs/development/agent-handoff.md` with the final Lite design and verification evidence.

## Deliverables

- Written architecture decision for Crabtya Lite versus full Crabtya.
- Separate Lite plugin/project or packaging path with full-loader surfaces excluded by construction.
- Native settings controls for scroll zoom/invert scroll and FOV.
- Lite release artifact under `packages/` with clear naming and install/uninstall guidance.
- User documentation comparing Crabtya Lite and full Crabtya.
- Handoff note with runtime verification evidence and any compatibility caveats.

## Verification

- Build: the Lite project/package builds cleanly without requiring full Crabtya mod discovery or `Crabtya.ModApi` DLL mod loading.
- Runtime: launch a clean game install with only Crabtya Lite installed.
- Runtime: confirm scroll zoom, invert scroll, and FOV settings appear in native-feeling settings surfaces and persist after restart.
- Runtime: confirm no `game/Mods/` discovery, `mod-state.json`, `startup-commands.json`, or full Mods settings window behavior occurs.
- Runtime: confirm base save IO remains in the normal game save location and Lite does not create/use `game/CrabtyaData/IsolatedSave/`.
- Runtime: confirm Lite code does not install the full Crabtya achievement/stat blocking hooks.
- Runtime: `game/BepInEx/ErrorLog.log` remains empty.

## Completion Criteria

- Players can choose between two clearly documented products:
  - full Crabtya for user-installed mods with isolation/safety features;
  - Crabtya Lite for curated QoL settings on the main install.
- Lite has no accidental path to arbitrary mod loading.
- The achievement/save behavior difference is intentional, documented, and runtime-verified.
- Full Crabtya v1 release artifacts and docs remain unaffected by Lite packaging.

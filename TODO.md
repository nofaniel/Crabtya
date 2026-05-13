# Notes

## Crabtya Lite - Post-v1 QoL Loader

After Crabtya v1 is complete and published, build a separate **Crabtya Lite** package for players who only want a tiny quality-of-life patch pack rather than a full user-mod loader.

Product shape:

- Small, easy, low-interference install on the main game install.
- Not a folder-based user mod loader.
- No user-installed `game/Mods/<mod-id>/` content, DLL entrypoints, manifests, dependency resolution, mod state, or mod settings menu.
- QoL features are pre-installed/bundled into the Lite loader package and controlled through native-feeling game settings only.
- Initial built-in features:
  - mouse-wheel zoom / scroll zoom option;
  - invert scroll option where applicable;
  - FOV option added to the native video/settings surface.
- Does not run on Crabtya's isolated save-space.
- Does not prevent Steam achievements or stats, because it must not run arbitrary user mods or broader gameplay/content patches.
- Should feel almost invisible: no full Crabtya Mods window, no runtime overlay, no mod enable/disable UI, and no debug controls.

Important boundaries:

- Full Crabtya remains the folder-based mod loader with save isolation and achievement blocking.
- Crabtya Lite must not weaken the safety contract of full Crabtya.
- If Lite ever grows beyond curated QoL settings into arbitrary mod/content loading, it must stop being achievement-safe and should use the full Crabtya safety model instead.
- Prefer a separate package/project or an explicit compile-time mode over a runtime flag that could accidentally leave full-loader behavior enabled.

Implementation planning target:

- Add this as the final post-v1 Job after the current v1 validation, packaging, and Git/GitHub readiness flow.
- First pass should be an architecture/product split spike: decide whether Lite is a separate BepInEx plugin project, a shared-core package with feature-specific applicators, or another packaging mode.
- Preserve/reuse proven QoL applicators where safe, but do not carry over full loader discovery, DLL mod API loading, user mod state, isolated save, achievement blocking, or Mods settings UI.

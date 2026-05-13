# Job 04 - Improve Crabtya Loader Branding

Source TODO paragraph: "Improve mod loader enabled branding - current is some small red text in the top right. Will create a mod loader brand image that can be added."

## Objective

Replace or augment the current minimal red text badge with a more polished native-feeling Crabtya branding treatment, while preserving the v1 UX boundary: small status label, native `MODS` button, and dedicated Mods settings window only. Branding should reassure users that Crabtya is loaded without becoming an intrusive overlay.

Final logo image files are available at repo root as `logo-L.png`, `logo-M.png`, and `logo-S.png`. Future branding passes should prefer those files for runtime brand treatment, docs screenshots, packaging, and GitHub-facing presentation, rather than older draft files under `loader/EIC.ModLoader/Assets/`.

## Scope

Work primarily in `loader/EIC.ModLoader/RuntimeOverlay.cs`, packaging/docs for any brand asset, and possibly `docs/users/install-uninstall.md` or screenshots if updated. Do not revive the old runtime overlay mods panel/debug controls.

## Implementation Plan

1. Define the target brand treatment: image badge, image plus text, or stylized native text. Keep it compact and main-menu scoped.
2. Decide asset ownership. If using an image, store it in a loader-owned path that can be packaged and loaded safely, such as a Crabtya data/assets folder or embedded resource, not inside original game assets.
3. If using a logo image, start from `logo-L.png`, `logo-M.png`, or `logo-S.png`; copy/package the selected asset into a loader-owned path and keep the root originals available for GitHub/README use.
4. Add a robust image-loading path with fallback to text if the asset is missing or fails to load.
5. Match game menu style: restrained size, native-looking color palette, no aggressive animation, no blocking raycasts, and no overlap with the injected `MODS` button.
6. Ensure branding does not appear during gameplay unless intentionally scoped by v1 docs. Current product direction favors main-menu status only.
7. Update installer/package scripts to include the branding asset if it is external.
8. Update docs and screenshots only after runtime proof confirms the final look.

## Deliverables

- Updated branding implementation with safe fallback.
- Brand asset in a loader-owned location if image-based.
- Package script updates if needed.
- Updated user/development docs noting the new branding surface.

## Status

Done (2026-05-13). Runtime-verified by user: tan/parchment panel with dark-brown "Crabtya v1.0.0" text appears in the top-right corner on the main menu, hides correctly during gameplay, and returns on menu. `ErrorLog.log` stayed empty.

## Verification

- Build: `dotnet build loader/EIC.ModLoader/EIC.ModLoader.csproj -c Release`.
- Runtime: launch game and confirm main menu shows the new Crabtya branding and the native `MODS` button still works.
- Confirm badge hides during gameplay and returns on main menu.
- Confirm `game/BepInEx/ErrorLog.log` remains empty.
- Confirm no old runtime overlay mods panel/debug controls appear.

## Completion Criteria

- The status branding feels intentional and native enough for public release.
- v1 UX remains narrow and non-invasive.
- `game/BepInEx/ErrorLog.log` stays empty after a full main-menu + gameplay + return session.

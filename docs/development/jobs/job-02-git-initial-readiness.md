# Job 02 - Prepare Repository for Initial Git/GitHub Publication

Source TODO paragraph: "Ensure directory is in state ready for git initial, I want a git page with clear info on the loader, how to install, current basic mods, and mod install process."

## Objective

Make the workspace safe and understandable for an initial git commit and public GitHub page, without accidentally publishing generated noise, private/local state, or mutable game runtime files that should not be source-controlled.

The target private GitHub repository is `https://github.com/nofaniel/Crabtya.git`. Once this Job confirms the repo is safe to initialize/publish, add the project to git and set that repository as the remote destination unless the user gives a different publishing instruction.

## Scope

Work at repo root plus `docs/`, `tools/`, `templates/`, `packages/`, `loader/`, and distributable sample mod docs. Treat the checked-in game install carefully: this repo currently contains the local game directory, but public GitHub readiness may require a deliberate `.gitignore`, README warnings, and a release-packaging story rather than committing every runtime artifact.

Use the finalized logo image files at repo root (`logo-L.png`, `logo-M.png`, `logo-S.png`) for README/GitHub presentation, release-page imagery, and any social-preview guidance. Prefer these over older draft brand assets under `loader/EIC.ModLoader/Assets/`.

## Implementation Plan

1. Decide the source-control boundary: which files are source, which are generated build outputs, which are local runtime state, and which game/vendor files should be ignored or excluded.
2. Add or update `.gitignore` for `.NET` build outputs, BepInEx logs/cache, mutable mod state, temporary screenshots, crash dumps, and local editor/tool files. Do not blindly ignore files that are required to build against the local game install unless the README explains how to restore them.
3. Create a root `README.md` that explains Crabtya in player-facing language: what it is, current status, install/uninstall, safe mode, mod folder layout, manifest basics, included sample mods, and where to find docs.
4. Add a clear "For mod makers" section pointing to `docs/mod-makers/quickstart.md`, `manifest-format.md`, templates, and the `Crabtya.ModApi.dll` contract.
5. Add a clear "For contributors/agents" section pointing to `AGENTS.md`, `PLAN.md`, `docs/development/jobs/`, architecture docs, and verification checklist.
6. Review `packages/README.md` and release zip naming so the GitHub page can link stable release artifacts without ambiguity.
7. If repository publication requires removing or quarantining local-only artifacts, propose the exact changes before deleting anything. Do not delete game binaries unless the user explicitly confirms that publication should exclude the local game install from the working tree.
8. Update `docs/README.md` quick links to include the Job plans and root README relationship.
9. After the safety review is complete, initialize git if needed, add the approved source/documentation/package files, and configure `origin` as `https://github.com/nofaniel/Crabtya.git`. Do not push until the staged file set has been reviewed for local game/runtime/private artifacts.

## Deliverables

- Root `README.md` suitable for GitHub.
- `.gitignore` suitable for a Windows game-loader workspace.
- Updated docs links where needed.
- A publication-readiness note in `docs/development/agent-handoff.md` listing any files that still need a human decision before `git init` / first commit.
- Git remote configuration for `https://github.com/nofaniel/Crabtya.git` once the repo is safe to initialize.

## Verification

- `git status --short` should be readable after `git init` is performed by the user or during a local dry run if already initialized.
- README install steps match `tools/Install-Crabtya.ps1`, `tools/Uninstall-Crabtya.ps1`, and current package layout.
- README mod install steps match `game/Mods/<mod-id>/eicmod.json` and user docs.
- No docs claim unsupported surfaces are production-ready beyond `PLAN.md`.

## Completion Criteria

- A new visitor can understand what Crabtya is, how to install it, how to install mods, and which sample mods exist.
- A contributor can tell what not to commit.
- Any remaining publication risks are explicitly called out instead of hidden.

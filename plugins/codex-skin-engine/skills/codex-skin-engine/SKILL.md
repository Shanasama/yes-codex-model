---
name: codex-skin-engine
description: Manage Codex pet skins, replace any of the nine animation-state GIFs, import or export .codexskin presets, apply a skin, and inspect the optional direct-GIF runtime bridge.
---

# Codex Skin Engine

Use the plugin's MCP tools for skin operations. Open the native studio window with `open_studio` when the user asks to browse, customize, upload, preview, or manage animations. It launches `dist\studio\SkinStudio.exe`; if the tool reports that the studio is not built yet, tell the user to run `build_studio.ps1` or double-click `启动皮肤工坊.cmd` once, because the first build takes a few minutes.

For direct requests:

- Use `list_skins` or `skin_status` for inspection.
- Use `import_skin` and `export_skin` for shareable `.codexskin` packages.
- Use `replace_state_gif` only for the requested state. The engine copies the source GIF byte-for-byte, checks its structure, records its frame count, and updates SHA-256.
- Use `apply_skin` when the user asks to install a chosen skin. Report the install path and any backup path from the result.
 - After `apply_skin`, tell the user to pick the skin once in Codex settings → Pets → Custom pets. Applying only writes the GIFs into the pet folder; it does not switch the active pet by itself. That pick is needed on the first install only: when the patched writable runtime is running, later applies show up on the running pet by themselves, because the patch tells the Codex UI to re-read the pet folder. Do not ask for a restart or a re-pick for those.
- Use `runtime_bridge` with `status` for diagnostics.

The direct-GIF bridge modifies a writable Codex `app.asar` overlay. Never call `runtime_bridge` with `install` or `restore` unless the user explicitly requests that mutation. The engine refuses unknown bundle versions and backs up the original header before installation. A full Codex restart is required after either operation.

Do not claim live animation continuity, activation, or GPU improvement from static validation alone. Activation needs the user to select the pet once in Codex settings → Pets → Custom pets, and the live update after that needs the patched writable runtime to be the one running - verify with the user instead of assuming.

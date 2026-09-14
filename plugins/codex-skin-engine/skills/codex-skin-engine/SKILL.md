---
name: codex-skin-engine
description: Manage Codex pet skins, replace any of the nine animation-state GIFs, import or export .codexskin presets, apply a skin, and inspect the optional direct-GIF runtime bridge.
---

# Codex Skin Engine

Use the plugin's MCP tools for skin operations. Open the visual editor with `open_studio` when the user asks to browse, customize, upload, preview, or manage animations.

For direct requests:

- Use `list_skins` or `skin_status` for inspection.
- Use `import_skin` and `export_skin` for shareable `.codexskin` packages.
- Use `replace_state_gif` only for the requested state. The engine copies the source GIF byte-for-byte, checks its structure, records its frame count, and updates SHA-256.
- Use `apply_skin` when the user asks to install a chosen skin. Report the install path and any backup path from the result.
- Use `runtime_bridge` with `status` for diagnostics.

The direct-GIF bridge modifies a writable Codex `app.asar` overlay. Never call `runtime_bridge` with `install` or `restore` unless the user explicitly requests that mutation. The engine refuses unknown bundle versions and backs up the original header before installation. A full Codex restart is required after either operation.

Do not claim live animation continuity, activation, or GPU improvement from static validation alone. Those require the user to restart Codex, select the pet, and observe the runtime.

# Codex Skin Engine

Lightweight local plugin for Codex pet skins and direct source-GIF animation mappings.

WineFox（酒狐）由哔哩哔哩 UP 主“酒石酸君”创作，原用于 Minecraft 的
Yes Steve Model（YSM）模组。本插件是第三方 Codex 适配，不是原作者、
YSM 或 OpenAI 的官方项目。软件代码采用 MIT License；内置 WineFox 素材
使用独立的 `LicenseRef-WineFox-Assets`，公开再分发前需取得原作者授权。

## Components

- `web/`: local visual editor served only on `127.0.0.1`
- `scripts/mcp-server.mjs`: Codex tools over stdio
- `scripts/lib/`: skin validation, ZIP safety, import/export, backup and apply
- `scripts/patch_codex_gif_runtime.mjs`: optional reversible bridge for writable Codex runtimes
- `presets/`: bundled data-only skins

No Codex executable, Electron runtime, `node_modules`, telemetry, or cloud upload is included.

The optional direct-GIF bridge supports only explicitly listed bundle hashes. It
operates on a user-writable shadow runtime, keeps a rollback backup, and does
not change WindowsApps ownership or ACLs.

## .codexskin

A `.codexskin` file is a ZIP with `skin.json`, one PNG/WebP fallback atlas, an optional thumbnail, and all nine GIF states. Executables, scripts, symlinks, absolute paths, traversal paths, encrypted entries, oversized entries, and invalid GIFs are rejected.

The engine stores imported skins under `%CODEX_HOME%\skin-engine\skins` or `%USERPROFILE%\.codex\skin-engine\skins`. Applied pets are staged atomically under the normal Codex `pets` directory.

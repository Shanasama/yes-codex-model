# codex皮肤插件

**这是干什么的**：给 Codex 桌面版的宠物换动画。装好之后，宠物在待机、工作、等待、跳跃
等九种状态里会播放你自己准备的原始 GIF —— 不转码、不缩放、不删帧，也不需要联网。仓库里
还带一个独立的皮肤工坊窗口程序，双击就能改皮肤、预览动画、导入导出皮肤包。

内置一套「酒狐（WineFox）」皮肤，素材来自 B 站 UP 主酒石酸君的 Minecraft YSM 皮肤，
本仓库是第三方 Codex 适配。

## 快速开始

### 1. 让 AI 装（推荐）

把这个仓库的 GitHub 链接和下面这段话一起发给 AI（Claude、ChatGPT、Codex 都行）：

```text
安装它：https://github.com/Shanasama/yes-codex-model
照仓库里的 AGENTS.md 一条龙装好，装完告诉我启动脚本在哪，我自己启动。
```

AI 会自己克隆仓库、检查环境、装好插件和完整 GIF 运行时，然后把启动脚本路径告诉你。
你只要完全退出 Codex，再双击它给的那个启动器。机器可读的步骤在 [AGENTS.md](AGENTS.md)。

### 2. 或者自己双击装

1. 把 ZIP 完整解压到任意目录，别在压缩包里直接双击。
2. 双击根目录的 `一键启动.cmd`。它会依次装好 Codex 插件、准备一份可写的 Codex 副本
   （第一次要复制约 1.8 GB，会慢几分钟）、应用酒狐皮肤，最后把 Codex 启动起来。
3. 想改皮肤就双击 `启动皮肤工坊.cmd`，第一次会先编译，要等几分钟。

只想做其中一步的话，`备用启动脚本\` 里有三个拆开的入口：

| 文件 | 作用 |
| --- | --- |
| `备用启动脚本\安装到 Codex.cmd` | 只装插件和酒狐皮肤 |
| `备用启动脚本\安装可写 GIF 运行时.cmd` | 只装或刷新可写 GIF 运行时 |
| `备用启动脚本\启动可写 GIF 运行时.cmd` | 启动已经装好的 GIF 版 Codex |

启动脚本会自己找仓库目录、Node.js、Codex 和 Windows 用户目录，不绑定作者电脑上的
用户名或盘符。

想先看看会发生什么、又不真的安装：

```powershell
$env:CODEX_SKIN_PLAN = "1"
& ".\一键启动.cmd"
```

## 皮肤工坊（独立窗口程序）

双击 `启动皮肤工坊.cmd`，或者直接运行 `dist\studio\SkinStudio.exe`。原生 WinForms 窗口，
不开浏览器、不需要联网。

第一次运行会调用 `build_studio.ps1` 编译，要等几分钟（会下载 .NET 运行时包）。编译结果
放在 `dist\studio\`：exe、随身的 Node 运行时、引擎脚本都在里面，整个文件夹拷到别的
Windows 电脑上可以直接双击运行。

工坊能做的事：皮肤库与搜索、动画实时预览（播放 / 暂停、适应 / 原始尺寸）、九张状态卡片
逐个替换 GIF（GIF 也可以直接拖到预览区）、编辑皮肤信息、复制 / 导出 / 导入 `.codexskin`、
一键应用到 Codex，以及完整动画运行时的一键开启与恢复。导入会拦掉脚本、可执行文件、
危险路径和异常大的压缩包。

## 完整 GIF 是怎么做的

Windows Store 版 Codex 的安装目录不能直接写入，所以脚本会把当前 Codex 复制到
`%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes`，只修改这份用户可写的副本。
脚本不会改 WindowsApps 的所有者或权限，原来的 Store 版 Codex 仍然保留。

第一次需要额外复制约 1.8 GB。补丁只认代码里记录过的 Codex 版本和 SHA-256：Codex 更新后
版本对不上就拒绝安装，而不是把新版程序改坏。被旧版本打过补丁的运行时可以原地升级。

## 环境要求

Windows 10/11、Codex 桌面版（要完整 GIF 播放就用 Microsoft Store 版）、Node.js 20 或更新
（也会自动找 Codex 自带的 Node）。第一次编译皮肤工坊还需要 .NET 7 SDK。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| `一键启动.cmd`、`install_one_click.ps1` | 一键入口，装插件 + 装运行时 + 启动 |
| `启动皮肤工坊.cmd`、`build_studio.ps1` | 编译并打开皮肤工坊 |
| `plugins\codex-skin-engine\` | Codex 插件：技能、MCP 工具、引擎脚本、内置皮肤 |
| `native\SkinStudio\` | 皮肤工坊的 C# 源码 |
| `qa\` | 自检脚本 |
| `归档\旧网页工坊\` | 以前的浏览器版界面，不再维护，只留作参考 |

## 开发者：自检

```powershell
.\dist\studio\SkinStudio.exe --selftest
node --test qa\bundle-syntax.test.mjs
$env:CODEX_SKIN_TEST_HOME = "qa/test-home"
node qa\core-check.mjs
```

---

# Codex Skin Engine

**What this is.** A tool that swaps Codex desktop pet animations for real GIFs. Once
installed, your pet plays your own source GIFs in all nine states - idle, working,
waiting, jumping and so on - with no transcoding, no scaling and no dropped frames, and
no network access. The repository also ships a standalone skin studio window so you can
edit skins, preview animations and import/export skin packs by double-clicking.

It bundles a "WineFox" skin created by Bilibili creator 酒石酸君 for Minecraft's YSM
mod; this repository is a third-party Codex adaptation.

## Quick start

### 1. Let an AI install it (recommended)

Send the repository link together with this text to any agent that can read the repo:

```text
Install it: <paste the GitHub link here>
Follow the repository's AGENTS.md end to end, then tell me where the launcher is.
I will start it myself.
```

The agent clones the repo, checks the environment, installs the plugin plus the full GIF
runtime, and reports the launcher path back to you. You then only have to fully quit Codex
and double-click that launcher. The machine-readable steps live in [AGENTS.md](AGENTS.md).

### 2. Or install it by double-clicking

1. Unzip the download into any folder - do not double-click inside the archive.
2. Double-click `一键启动.cmd` in the repository root. It installs the Codex plugin,
   prepares a writable copy of Codex (the first run copies about 1.8 GB, so give it a few
   minutes), applies the WineFox skin and finally starts Codex.
3. To edit skins, double-click `启动皮肤工坊.cmd`; the first run compiles it, which takes
   a few minutes.

If you only need part of the flow, the `备用启动脚本\` ("backup launchers") folder has
three split entries:

| File | What it does |
| --- | --- |
| `备用启动脚本\安装到 Codex.cmd` | Installs the plugin and the WineFox skin only |
| `备用启动脚本\安装可写 GIF 运行时.cmd` | Installs or refreshes the writable GIF runtime |
| `备用启动脚本\启动可写 GIF 运行时.cmd` | Starts an already installed GIF Codex |

Every launcher finds the repository root, Node.js, Codex and the Windows user directories
by itself, so nothing is tied to the author's username or drive letter.

To see what the installer would do without changing anything:

```powershell
$env:CODEX_SKIN_PLAN = "1"
& ".\一键启动.cmd"
```

## Skin studio (standalone window app)

Double-click `启动皮肤工坊.cmd`, or run `dist\studio\SkinStudio.exe` directly. It is a
native WinForms window: no browser, no network.

The first run calls `build_studio.ps1` and takes a few minutes (it downloads a .NET runtime
pack). The result in `dist\studio\` already contains the exe, a bundled Node runtime and
the engine scripts, so the whole folder can be copied to another Windows machine and run
by double-clicking.

The studio does: skin library and search, live animation preview (play/pause, fit/original
size), nine state cards with per-state GIF replacement (you can also drop a GIF onto the
preview), skin metadata editing, duplicate/export/import of `.codexskin`, one-click apply
to Codex, and install/restore of the full animation runtime. Imports reject scripts,
executables, dangerous paths and oversized archives.

## How the full GIF playback works

The Microsoft Store build of Codex lives in a folder that cannot be written to, so the
installer copies the current Codex into
`%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes` and only modifies that
user-writable copy. It never changes the owner or the ACLs of WindowsApps, and the
original Store build stays untouched.

The first run copies about 1.8 GB. The patch only accepts the Codex versions and SHA-256
hashes recorded in the source; after a Codex update it refuses to install instead of
breaking a newer build. Runtimes patched by an older version of this project are upgraded
in place.

## Requirements

Windows 10/11, Codex Desktop (use the Microsoft Store build for full GIF playback), and
Node.js 20 or newer - the installers also find the Node bundled with Codex. The first
studio build additionally needs the .NET 7 SDK.

## Repository layout

| Path | Contents |
| --- | --- |
| `一键启动.cmd`, `install_one_click.ps1` | Main entry: plugin + runtime + launch |
| `启动皮肤工坊.cmd`, `build_studio.ps1` | Build and open the skin studio |
| `plugins\codex-skin-engine\` | The Codex plugin: skill, MCP tools, engine scripts, bundled skin |
| `native\SkinStudio\` | C# source of the studio window |
| `qa\` | Self-check scripts |
| `归档\旧网页工坊\` | The retired browser UI, kept for reference only |

## Developers: self-checks

```powershell
.\dist\studio\SkinStudio.exe --selftest
node --test qa\bundle-syntax.test.mjs
$env:CODEX_SKIN_TEST_HOME = "qa/test-home"
node qa\core-check.mjs
```

# codex皮肤插件

**这是干什么的**：给 Codex 桌面版的宠物换动画。装好之后，宠物在待机、工作、等待、跳跃
等九种状态里会播放你自己准备的原始 GIF —— 不转码、不缩放、不删帧，也不需要联网。仓库里
还带一个独立的皮肤工坊窗口程序，双击就能改皮肤、预览动画、导入导出皮肤包。

内置一套「酒狐（WineFox）」皮肤，素材来自 B 站 UP 主酒石酸君的 Minecraft YSM 皮肤，
本仓库是第三方 Codex 适配。

> **它会动我的 Codex 吗？** 会，但只动副本：安装时把你的 Codex 整份复制到
> `%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes\`（实测约 1.8 GB），之后只在
> 这份可写副本里改。原版 Store 版 Codex 一个字节不动，权限、所有者、文件都保持原样。
> 不想要了，退出 Codex 后把这个目录整个删掉就回到原样。为什么必须这么做见
> [下面那节](#会复制一份-codex以及为什么)。

## 快速开始

### 1. 让 AI 装（推荐）

把这个仓库的 GitHub 链接和下面这段话一起发给 AI（Claude、ChatGPT、Codex 都行）：

```text
安装它：https://github.com/Shanasama/yes-codex-model
先读仓库里的 AGENTS.md，按里面的步骤先做环境体检（Node、.NET SDK、磁盘空间），
再一条龙装好。装完把体检结果和启动脚本路径告诉我，我自己启动。
```

AI 会自己克隆仓库、先跑一次环境体检（Node、.NET SDK、磁盘空间），再装好插件和完整 GIF
运行时，然后把体检结果和启动脚本路径告诉你。
你只要完全退出 Codex，双击它给的那个启动器，再去 Codex 设置 → 宠物 → 自定义宠物 里选中
这套皮肤（只有第一次要选）。机器可读的步骤在 [AGENTS.md](AGENTS.md)。

### 2. 或者自己双击装

1. 把 ZIP 完整解压到任意目录，别在压缩包里直接双击。
2. 双击根目录的 `一键启动.cmd`。它会依次装好 Codex 插件、准备一份可写的 Codex 副本
   （第一次要复制约 1.8 GB，会慢几分钟）、应用酒狐皮肤，最后把 Codex 启动起来。
3. 然后完全退出 Codex，用启动器重新打开，再到 Codex 设置 → 宠物 → 自定义宠物 里选中
   「酒狐（WineFox）」—— 这一次手动选中只有第一次要；以后在工坊里改皮肤，正在跑的宠物
   会自己换上。
4. 想改皮肤就双击 `启动皮肤工坊.cmd`，第一次会先编译，要等几分钟（这一步需要 .NET 7
   SDK，缺的话脚本会直接告诉你怎么补）。

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

编译需要 .NET 7 SDK，只有 .NET 运行时是不够的。缺 SDK 时脚本不会假装成功，而是直接
告诉你装哪个版本，或者让你把 SDK 的 ZIP 解压到仓库的 `.dotnet-sdk\`（里面要有
`dotnet.exe`），然后双击 `启动皮肤工坊.cmd` 重试就行，不用重装插件。

工坊能做的事：皮肤库与搜索、动画实时预览（播放 / 暂停、适应 / 原始尺寸）、九张状态卡片
逐个替换 GIF（GIF 也可以直接拖到预览区）、编辑皮肤信息、复制 / 导出 / 导入 `.codexskin`、
一键应用到 Codex、删除皮肤库里的条目（素材移到备份目录，可以找回），以及完整动画运行时
的一键开启与恢复。导入会拦掉脚本、可执行文件、
危险路径和异常大的压缩包。

应用后会立刻生效：装好完整动画运行时之后，九张 GIF 一写进 Codex 的宠物目录，正在跑的宠物
就会自己换上新的动作 —— 不用重启，也不用切到别的宠物再切回来。只有**第一次**安装时要去
Codex 设置 → 宠物 → 自定义宠物 里选中它一次；之后在工坊里换皮肤、换动作都是自动的。
工坊的应用确认框、成功提示和右侧检查器都写了这一点。

## 会复制一份 Codex（以及为什么）

Codex 桌面版（Microsoft Store 版）装在 `C:\Program Files\WindowsApps\...` 里，这个目录
Windows 只给读权限：往里写会被系统直接拒绝，而且它受 Store 的完整性保护，就算绕过权限改
掉了，下次 Codex 一更新，改动也会被整份覆盖回去。

所以想让它按 GIF 动，只能绕开原版：

1. 把当前版本的 Codex **整份复制**到用户目录
   `%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes\store-<版本号>\`，实测约
   1.8 GB、五千多个文件，第一次要等几分钟。
2. 只改这份副本里的两个打包 JS（`app.asar` 里的主进程和渲染层），让它按宠物目录里
   `pet.json` 的 `animationPaths` 播放九种状态的 GIF。
3. 以后启动的是这份副本（`一键启动.cmd` 的最后一步，或
   `备用启动脚本\启动可写 GIF 运行时.cmd`）。Store 版 Codex 还在原地，照常能打开。

你可能关心的几点：

- **原版没被动过**：脚本不改 WindowsApps 的权限和所有者，只是读它、然后复制。
- **能反悔**：退出 Codex 后删掉 `shadow-runtimes` 目录就干净了；皮肤工坊里的
  「恢复兼容模式」也能把副本还原成没打过补丁的状态。
- **占地方**：一份约 1.8 GB。Codex 升级后版本号变了会再复制一份新的，旧的那份可以自己删。
- **不是随便哪个版本都能改**：补丁只认代码里记录过的版本和 SHA-256，Codex 更新后如果
  不认识就直接拒绝安装，而不是把新版程序改坏。被旧版本打过补丁的运行时可以原地升级。
- **改了皮肤立刻生效**：补丁会在宠物目录有变化时叫 Codex 界面重新读盘，所以在工坊里换完
  动作，正在跑的宠物当场就变，不用重启，也不用切换宠物。

## 环境要求

Windows 10/11、Codex 桌面版（要完整 GIF 播放就用 Microsoft Store 版）、Node.js 20 或更新
（也会自动找 Codex 自带的 Node）。第一次编译皮肤工坊还需要 .NET 7 SDK；没有的话插件、
皮肤和 GIF 运行时照样装好，安装脚本会明确报「部分完成」并告诉你补哪一步。

想自己先体检，不改任何文件：

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -Plan
```

输出的 JSON 里 `environment` 一节就是体检结果：Node 版本、.NET SDK 有没有、工坊编译了没、
用户目录所在盘剩多少空间，以及需要提前告诉用户的 `warnings`。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| `一键启动.cmd`、`install_one_click.ps1` | 一键入口，装插件 + 装运行时 + 启动 |
| `启动皮肤工坊.cmd`、`build_studio.ps1` | 编译并打开皮肤工坊 |
| `plugins\codex-skin-engine\` | Codex 插件：技能、MCP 工具、引擎脚本、内置皮肤 |
| `native\SkinStudio\` | 皮肤工坊的 C# 源码 |
| `qa\` | 自检脚本 |
| `归档\` | 退役的旧网页工坊，不参与任何安装流程；为什么留着见 `归档\说明.md` |

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

> **Does it touch my Codex?** Yes, but only a copy. The installer duplicates your Codex
> into `%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes\` (about 1.8 GB in
> practice) and patches only that writable copy. The Microsoft Store install stays
> byte-for-byte untouched - same files, same permissions, same owner. To undo everything,
> quit Codex and delete that folder, and you are back to the stock app. The reason for the
> copy is in [the section below](#it-copies-codex-and-why).

## Quick start

### 1. Let an AI install it (recommended)

Send the repository link together with this text to any agent that can read the repo:

```text
Install it: <paste the GitHub link here>
Read the repository's AGENTS.md first and run the environment check-up it describes
(Node, .NET SDK, free disk space), then follow it end to end.
Tell me the check-up result and where the launcher is. I will start it myself.
```

The agent clones the repo, runs an environment check-up (Node, .NET SDK, free disk space),
installs the plugin plus the full GIF runtime, and reports the check-up result together
with the launcher path. You then only have to fully quit Codex and double-click that
launcher, and finally pick the skin in Codex settings → Pets → Custom pets (first install
only). The machine-readable steps live in [AGENTS.md](AGENTS.md).

### 2. Or install it by double-clicking

1. Unzip the download into any folder - do not double-click inside the archive.
2. Double-click `一键启动.cmd` in the repository root. It installs the Codex plugin,
   prepares a writable copy of Codex (the first run copies about 1.8 GB, so give it a few
   minutes), applies the WineFox skin and finally starts Codex.
3. Then fully quit Codex, start it again with the launcher, and pick the skin in Codex
   settings → Pets → Custom pets - that one pick is needed only the first time. After that,
   editing skins in the studio changes the running pet right away.
4. To edit skins, double-click `启动皮肤工坊.cmd`; the first run compiles it, which takes
   a few minutes and needs the .NET 7 SDK.

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

Building needs the .NET 7 SDK; the .NET runtime alone is not enough. If the SDK is missing
the script does not pretend to succeed: it tells you which SDK to install, or to unzip the
SDK ZIP into the repository's `.dotnet-sdk\` folder (it must contain `dotnet.exe`), and then
to double-click `启动皮肤工坊.cmd` again. No plugin reinstall is needed.

The studio does: skin library and search, live animation preview (play/pause, fit/original
size), nine state cards with per-state GIF replacement (you can also drop a GIF onto the
preview), skin metadata editing, duplicate/delete/export/import of `.codexskin`, one-click
apply to Codex, and install/restore of the full animation runtime. Deleted skins move to the
backup folder instead of being destroyed. Imports reject scripts,
executables, dangerous paths and oversized archives.

Applying takes effect immediately: once the full animation runtime is installed, writing the
nine GIFs into the Codex pet folder makes the running pet switch to the new animations by
itself - no restart, and no switching to another pet and back. Only the **first** install needs
one manual step: pick the skin in Codex settings → Pets → Custom pets. Later skin or animation
changes made in the studio apply on their own. The studio's confirm dialog, success toast and
inspector all say so.

## It copies Codex (and why)

Codex Desktop from the Microsoft Store lives in `C:\Program Files\WindowsApps\...`, which
Windows only exposes read-only: writes are rejected outright, and the folder is covered by
Store integrity checks, so even a forced edit would be wiped by the next Codex update.

That leaves one way to get real GIF animations:

1. **Copy the whole current Codex** into the user profile at
   `%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes\store-<version>\` - about
   1.8 GB and 5000+ files, so the first run takes a few minutes.
2. Patch only that copy: two bundled JS files inside `app.asar` (main process and renderer)
   so the pet plays GIFs from `animationPaths` in `pet.json` for all nine states.
3. Launch that copy from then on (the last step of `一键启动.cmd`, or
   `备用启动脚本\启动可写 GIF 运行时.cmd`). The Store build stays where it is and still
   opens normally.

Worth knowing:

- **Your install is untouched.** The scripts never change WindowsApps permissions or
  ownership; they only read and copy it.
- **It is reversible.** Quit Codex and delete the `shadow-runtimes` folder, or use restore
  compatibility mode in the studio to unpatch the copy.
- **It costs disk.** About 1.8 GB per Codex version. A Codex update creates a new
  `store-<version>` folder; older ones can be deleted by hand.
- **Only known builds are patched.** The patch accepts only the Codex versions and SHA-256
  hashes recorded in the source; after an update it refuses to install instead of breaking
  a newer build. Runtimes patched by older releases are upgraded in place.
- **Skin edits show up live.** The patch makes the Codex UI re-read the pet folder whenever it
  changes, so a running pet switches to your new animations right away - no restart, no pet
  switching.

## Requirements

Windows 10/11, Codex Desktop (use the Microsoft Store build for full GIF playback), and
Node.js 20 or newer - the installers also find the Node bundled with Codex. The first
studio build additionally needs the .NET 7 SDK.

To run the check-up yourself, without changing anything:

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -Plan
```

The `environment` section of that JSON is the check-up: Node version, whether a .NET SDK
exists, whether the studio is already built, free space on the profile drive, and the
`warnings` worth telling the user about up front.

## Repository layout

| Path | Contents |
| --- | --- |
| `一键启动.cmd`, `install_one_click.ps1` | Main entry: plugin + runtime + launch |
| `启动皮肤工坊.cmd`, `build_studio.ps1` | Build and open the skin studio |
| `plugins\codex-skin-engine\` | The Codex plugin: skill, MCP tools, engine scripts, bundled skin |
| `native\SkinStudio\` | C# source of the studio window |
| `qa\` | Self-check scripts |
| `归档\` | The retired browser studio, not part of any install path; see `归档\说明.md` |

## Developers: self-checks

```powershell
.\dist\studio\SkinStudio.exe --selftest
node --test qa\bundle-syntax.test.mjs
$env:CODEX_SKIN_TEST_HOME = "qa/test-home"
node qa\core-check.mjs
```

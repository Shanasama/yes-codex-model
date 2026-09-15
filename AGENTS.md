# AI 安装说明 / AI Install Guide

用户把本仓库链接发给你并要求安装时，按下面执行。你的任务到“告诉用户启动器在
哪里”为止，不要替用户启动 Codex。

中文说明在前，完整英文版在后。

## 中文

### 环境要求

- Windows 10 / 11
- 已安装 Codex 桌面版（完整 GIF 运行时需要 Microsoft Store 版）
- Node.js 20 或更新；脚本也会自动查找 Codex 自带的 Node
- 编译皮肤工坊窗口程序需要 .NET 7 SDK（只有 .NET 运行时不够；缺 SDK 时安装脚本会给出
  中文提示并以「部分完成」结束，插件和皮肤不受影响）

### 安装步骤

1. 把仓库放到用户可写的固定目录。不要用临时目录，也不要事后删除，插件会一直
   引用这个路径：

```powershell
git clone <仓库地址> "$env:USERPROFILE\codex-skin-engine"
cd "$env:USERPROFILE\codex-skin-engine"
```

没有 git 就下载 ZIP 解压到同样位置。

2. 先检查环境，并且把体检结果告诉用户。这一步不改任何文件：

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -Plan
```

输出 JSON 里的 `environment` 就是体检结果，逐项对照：

| 字段 | 含义 | 不合格怎么办 |
| --- | --- | --- |
| `node.ok`、`node.version` | Node.js 是不是 20 以上（脚本也会用 Codex 自带的 Node） | 装 Node.js LTS 20+，或先启动一次 Codex |
| `dotnetSdk.ok`、`dotnetSdk.versions` | 有没有 .NET SDK（只装运行时不等于有） | 缺就别承诺“装完就能改皮肤”：插件、皮肤、GIF 运行时照样装，工坊要等补 .NET 7 SDK，或把 SDK 的 ZIP 解压到 `<仓库>\.dotnet-sdk\`（里面要有 `dotnet.exe`） |
| `studioBuilt` | `dist\studio\SkinStudio.exe` 是不是已经编译好 | 它是 `false` 且 `dotnetSdk.ok` 也是 `false` 时，明确告诉用户这次工坊装不上 |
| `freeSpace.gb` | 用户目录所在卷的剩余空间 | 低于 3 GB 先提醒用户：完整 GIF 运行时要再复制一份约 1.8 GB 的 Codex |
| `package`、`version` | 有没有 Microsoft Store 版 Codex | 没有就改走下面第 4 步的 `-PluginOnly`，并说明完整 GIF 播放需要 Store 版 |

体检结果要写进汇报里（例如「Node 24 ✓、没有 .NET SDK ✗、C 盘剩 80 GB ✓」），
`environment.warnings` 里的每一条都要先跟用户解释清楚，再开始装。

3. 正式安装。免交互、不关 Codex、不启动 Codex：

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -NoPrompt -NoLaunch -SkipStoreCheck
```

首次会复制约 1.8GB 的 Codex 到
`%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes`，需要几分钟，等待它跑完。

4. 如果用户只要插件和皮肤、不要完整 GIF 运行时，改用：

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -PluginOnly -NoPrompt
```

### 安装完成后对用户说什么

确认输出里没有报错，然后告诉用户：

- 已经装好了什么
- 启动器的绝对路径：`<仓库目录>\备用启动脚本\启动可写 GIF 运行时.cmd`
- 启动前要完全退出 Codex，然后双击这个启动器
 - **第一次的最后一步（必须说）**：重启 Codex 后，去 Codex 设置 → 宠物 → 自定义宠物 里
   选中刚应用的皮肤，宠物才会变；不选的话看起来跟没装一样
 - 只有第一次需要这一步：补丁会在宠物目录变化时自动刷新界面，所以选中过一次之后，在工坊里
   换皮肤、换动作会立刻生效，不用重启、也不用切换宠物
- 皮肤工坊窗口程序的路径：`<仓库目录>\dist\studio\SkinStudio.exe`，双击就能改皮肤

只安装插件时，告诉用户双击根目录的 `启动皮肤工坊.cmd`，并新建一个 Codex
任务让插件生效。

安装脚本最后会顺手编译一次皮肤工坊窗口程序，编好的 `dist\studio\` 里带着 exe、
Node 运行时和引擎脚本，整个文件夹可以拷到别的电脑直接双击运行。如果编译失败，脚本会
打印「部分完成」并以退出码 2 结束：这时不要说“全套装好了”，要说明插件/皮肤/运行时已经
可用、只有工坊没编译，把真实报错交给用户，让他按提示补 .NET 7 SDK 后双击
`启动皮肤工坊.cmd` 重试（不用重装插件）。

### 注意事项

- 不要在 AI 会话里运行根目录的 `一键启动.cmd`，它会关闭 Store 版
  Codex，也就是你自己所在的程序。
- 不要声称已经启动成功，启动必须由用户手动完成。
 - 别把“选中宠物”说漏：第一次装好后用户必须在 Codex 设置 → 宠物 → 自定义宠物 里选中它。
   但也别把后面的改动说成要重启：选中过一次之后，换皮肤/换动作是自动生效的。
- 失败时把真实报错原文交给用户，不要编造成功结果。
- 找不到 Store 版 Codex 时用 `-PluginOnly`，并说明完整 GIF 播放需要 Store 版。
- 酒狐素材由哔哩哔哩 UP 主“酒石酸君”创作，原用于 Minecraft 的 Yes Steve
  Model（YSM）模组。不要描述成本项目或 OpenAI 的官方项目。

## English

Send this repository link to your AI and ask it to install it. Your AI follows
the steps below, reports the launcher path, and stops there. It must not start
Codex for the user.

### Requirements

- Windows 10 / 11
- Codex Desktop installed. The Microsoft Store build is required for full GIF
  playback.
- Node.js 20 or newer. The installer also finds the Node bundled with Codex.
- Building the Skin Studio window app needs the .NET 7 SDK; the .NET runtime
  alone is not enough. Without the SDK the installer reports "partial install"
  and explains what to do; the plugin and the skin are unaffected.

### Steps

1. Put the repository in a fixed, user-writable folder. Do not use a temp folder
   and do not delete it afterwards, because the plugin keeps referencing this
   path:

```powershell
git clone <repository-url> "$env:USERPROFILE\codex-skin-engine"
cd "$env:USERPROFILE\codex-skin-engine"
```

If git is unavailable, download the ZIP and extract it to the same location.

2. Check the environment first and report the result to the user. This step
   changes nothing:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -Plan
```

The `environment` object in that JSON is the check-up. Go through it:

| Field | Meaning | What to do when it fails |
| --- | --- | --- |
| `node.ok`, `node.version` | Node.js 20 or newer (the scripts also find the Node bundled with Codex) | Install Node.js LTS 20+, or start Codex once |
| `dotnetSdk.ok`, `dotnetSdk.versions` | Whether a .NET SDK exists (a runtime alone is not enough) | Do not promise skin editing in that case: the plugin, skin and GIF runtime still install, but the studio has to wait for the .NET 7 SDK, or for the SDK ZIP to be unzipped into `<repository>\.dotnet-sdk\` (it must contain `dotnet.exe`) |
| `studioBuilt` | Whether `dist\studio\SkinStudio.exe` is already built | When this is `false` and `dotnetSdk.ok` is also `false`, tell the user the studio cannot be installed this time |
| `freeSpace.gb` | Free space on the volume that holds the user profile | Below 3 GB, warn the user: the full GIF runtime copies another ~1.8 GB of Codex |
| `package`, `version` | Whether the Microsoft Store build of Codex is present | If not, switch to `-PluginOnly` in step 4 and say that full GIF playback needs the Store build |

Put the result in your report (for example "Node 24 OK, no .NET SDK, 80 GB free on C:")
and explain every entry in `environment.warnings` before you start installing.

3. Install. Non-interactive, does not close Codex, does not start Codex:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -NoPrompt -NoLaunch -SkipStoreCheck
```

The first run copies about 1.8GB of Codex into
`%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes`. It takes a few
minutes; let it finish.

4. If the user only wants the plugin and skin, without the full GIF runtime:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -PluginOnly -NoPrompt
```

### What to tell the user when it is done

Check that the output contains no errors, then tell the user:

- what was installed
- the absolute path of the launcher: `<repository folder>\备用启动脚本\启动可写 GIF 运行时.cmd`
- they must fully quit Codex before double-clicking that launcher
 - **the first-time last step, always mention it**: after restarting Codex, pick the skin
   in Codex settings → Pets → Custom pets, otherwise the pet looks unchanged
 - it is only needed once: the patch refreshes the UI when the pet folder changes, so after
   that first pick, editing skins or animations in the studio takes effect right away - no
   restart, no switching pets
- the Skin Studio app path: `<repository folder>\dist\studio\SkinStudio.exe`,
  which is a standalone window for editing skins

For a plugin-only install, tell the user to double-click `启动皮肤工坊.cmd` in the
repository root, and to open a new Codex task so the plugin loads.

The installer also builds the Skin Studio window app once at the end. The resulting
`dist\studio\` folder contains the exe, a Node runtime, and the engine scripts, so it
can be copied to another machine and started by double-clicking. If that build fails,
the installer prints "partial install" and exits with code 2. Do not claim a full
success: say that the plugin, skin and runtime are usable but the studio was not built,
report the real error, and tell the user to install the .NET 7 SDK and double-click
`启动皮肤工坊.cmd` to retry (no plugin reinstall needed).

### Rules

- Never run the root `一键启动.cmd` inside an AI session. It closes the
  Store build of Codex, which is the program you are running in.
- Never claim that Codex was started. Starting it is the user's job.
 - Never leave out the pet pick: on the first install the user must select the skin in
   Codex settings → Pets → Custom pets. But do not tell them to restart for later changes:
   once the skin has been picked, skin and animation edits apply by themselves.
- On failure, give the user the real error text. Do not invent a success result.
- When no Microsoft Store build of Codex is found, use `-PluginOnly` and say that
  full GIF playback needs the Store build.
- The WineFox assets were created by the Bilibili creator 酒石酸君 for Minecraft's
  Yes Steve Model (YSM) mod. Never describe this project as official.

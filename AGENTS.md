# AI 安装说明 / AI Install Guide

用户把本仓库链接发给你并要求安装时，按下面执行。你的任务到“告诉用户启动器在
哪里”为止，不要替用户启动 Codex。

中文说明在前，完整英文版在后。

## 中文

### 环境要求

- Windows 10 / 11
- 已安装 Codex 桌面版（完整 GIF 运行时需要 Microsoft Store 版）
- Node.js 20 或更新；脚本也会自动查找 Codex 自带的 Node
- 编译皮肤工坊窗口程序需要 .NET 7 SDK（没有的话安装脚本只会跳过这一步并给出提示）

### 安装步骤

1. 把仓库放到用户可写的固定目录。不要用临时目录，也不要事后删除，插件会一直
   引用这个路径：

```powershell
git clone <仓库地址> "$env:USERPROFILE\codex-skin-engine"
cd "$env:USERPROFILE\codex-skin-engine"
```

没有 git 就下载 ZIP 解压到同样位置。

2. 先看会做什么，这一步不改任何文件：

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -Plan
```

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
- 皮肤工坊窗口程序的路径：`<仓库目录>\dist\studio\SkinStudio.exe`，双击就能改皮肤

只安装插件时，告诉用户双击根目录的 `启动皮肤工坊.cmd`，并新建一个 Codex
任务让插件生效。

安装脚本最后会顺手编译一次皮肤工坊窗口程序，编好的 `dist\studio\` 里带着 exe、
Node 运行时和引擎脚本，整个文件夹可以拷到别的电脑直接双击运行。如果编译失败，
不要宣称装好了，把真实报错交给用户，并让他双击 `启动皮肤工坊.cmd` 重试。

### 注意事项

- 不要在 AI 会话里运行根目录的 `一键启动.cmd`，它会关闭 Store 版
  Codex，也就是你自己所在的程序。
- 不要声称已经启动成功，启动必须由用户手动完成。
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
- Building the Skin Studio window app needs the .NET 7 SDK. Without it the
  installer only skips that step and says so.

### Steps

1. Put the repository in a fixed, user-writable folder. Do not use a temp folder
   and do not delete it afterwards, because the plugin keeps referencing this
   path:

```powershell
git clone <repository-url> "$env:USERPROFILE\codex-skin-engine"
cd "$env:USERPROFILE\codex-skin-engine"
```

If git is unavailable, download the ZIP and extract it to the same location.

2. Preview what will happen. This step changes nothing:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\install_one_click.ps1 -Plan
```

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
- the Skin Studio app path: `<repository folder>\dist\studio\SkinStudio.exe`,
  which is a standalone window for editing skins

For a plugin-only install, tell the user to double-click `启动皮肤工坊.cmd` in the
repository root, and to open a new Codex task so the plugin loads.

The installer also builds the Skin Studio window app once at the end. The resulting
`dist\studio\` folder contains the exe, a Node runtime, and the engine scripts, so it
can be copied to another machine and started by double-clicking. If that build fails,
do not claim success: report the real error and tell the user to double-click
`启动皮肤工坊.cmd` to retry.

### Rules

- Never run the root `一键启动.cmd` inside an AI session. It closes the
  Store build of Codex, which is the program you are running in.
- Never claim that Codex was started. Starting it is the user's job.
- On failure, give the user the real error text. Do not invent a success result.
- When no Microsoft Store build of Codex is found, use `-PluginOnly` and say that
  full GIF playback needs the Store build.
- The WineFox assets were created by the Bilibili creator 酒石酸君 for Minecraft's
  Yes Steve Model (YSM) mod. Never describe this project as official.

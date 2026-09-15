# AI 安装说明 / AI Install Guide

用户把本仓库链接发给你并要求安装时，按下面执行。你的任务到“告诉用户启动器在
哪里”为止，不要替用户启动 Codex。

**这份文件是任务书，不是操作手册。** 下面写清了三件事：要达成什么、边界在哪、仓库里
有哪些现成工具。具体怎么组合、先做哪步、出问题怎么绕，你自己判断；但边界不许越。

遇到本文件没写的情况，先跑只读探测（见「Codex 更新了怎么办」），拿到事实再决定，
不要凭猜动手，也不要为了让流程走通而绕开安全检查。

中文说明在前，完整英文版在后。

## 中文

### 环境要求

- Windows 10 / 11
- 已安装 Codex 桌面版（完整 GIF 运行时需要 Microsoft Store 版）
- Node.js 20 或更新；脚本也会自动查找 Codex 自带的 Node
- 编译皮肤工坊窗口程序需要 .NET 7 SDK（只有 .NET 运行时不够；缺 SDK 时安装脚本会给出
  中文提示并以「部分完成」结束，插件和皮肤不受影响）

### 两种档位，先问清楚再装

| 档位 | 做法 | 效果 | 代价 |
| --- | --- | --- | --- |
| **通用档**（推荐默认） | 只用官方图集接口，不打补丁 | 九种状态逐帧播放，Codex 怎么更新都不受影响 | 每种状态最多 4～8 帧，帧间隔由官方定 |
| **保真档** | 在 Codex 副本里打补丁，直接播原始 GIF | 不转码、不缩放、不删帧（267 帧也不会被压成 6 帧） | 依赖具体版本，Codex 更新后可能要重新适配 |

用户没明说就按通用档；说了要「原始 GIF 效果」「不要掉帧」这类再上保真档，并且要提前告知
保真档的代价。两个档位可以同时存在，装完再切换也是允许的。

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

### Codex 更新了怎么办

补丁只认识记录过的版本和 SHA-256，官方一更新就可能失效 —— 这不是故障，是这套方案的
固有代价。别猜，按下面顺序拿事实：

1. 只读探测，不改任何文件：

```powershell
node qa\patch-dry-run.mjs
```

   它会自己找 Store 版 Codex，输出：`verdict` 是 `patchable`（能打）、`already-patched`
   （已打）、`legacy`（旧补丁，启动器会原地升级）、`unsupported`（不认识这个版本）还是
   `broken`（认识但改不动）。同时给出两个 bundle 的 `archivePath` 和 `sha256`。
2. `unsupported` / `broken` 时：把新版 bundle 抽出来看结构有没有变（`qa\inspect-asar-targets.mjs`
   和 `qa\read-asar-snippets.mjs`），比对 `TARGET_PROFILES` 里的锚点；新版只是重编译、锚点
   没变的话，通常只要补一条 profile（渲染层文件名 + 基线哈希）和状态判定。
3. 真改不动就和用户说实话：保真档这个版本先不上，退回通用档（官方图集接口，跟版本无关），
   或者等适配。**不要**为了让安装跑完而放宽哈希校验、跳过语法检查、或者声称成功。

改完适配后必须验证：`--action plan` → `--action apply` → `--action verify` 全过，且
`--action restore` 能干净回到 `baseline`。

### 改这个仓库时的规矩

用户可能让你改代码而不只是安装，改完必须自检，缺一项都别提交：

```powershell
.\dist\studio\SkinStudio.exe --selftest      # 工坊布局自检，要求 failures=0
node --test qa\bundle-syntax.test.mjs        # 补丁替换块的语法检查
node --test qa\hot-reload.test.mjs           # 热更新注入与查询失效
$env:CODEX_SKIN_TEST_HOME = "qa/test-home"; node qa\core-check.mjs   # 引擎增删改查
```

改完插件本体（`plugins\codex-skin-engine\`）要刷缓存版本号再重装，否则 Codex 还在用旧副本：

```powershell
python C:\Users\yuan\.codex\skills\.system\plugin-creator\scripts\update_plugin_cachebuster.py .\plugins\codex-skin-engine
codex plugin add codex-skin-engine@personal
```

还有两条红线：**别改用户已有的备份路径**（`skin-engine\backups\` 下面那些是用户的退路），
**别把删除做成真删**（皮肤删除是移到备份目录，不是 `rm`）。

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

**This file is a brief, not a checklist.** It states the goal, the hard limits, and the
tools the repository already ships. How to combine them, what to do first, and how to work
around a failure is your call - but the limits are not.

When the situation is not covered here, run the read-only probe (see "After a Codex update"),
get facts first, and do not guess or bypass a safety check just to make the flow finish.

### Requirements

- Windows 10 / 11
- Codex Desktop installed. The Microsoft Store build is required for full GIF
  playback.
- Node.js 20 or newer. The installer also finds the Node bundled with Codex.
- Building the Skin Studio window app needs the .NET 7 SDK; the .NET runtime
  alone is not enough. Without the SDK the installer reports "partial install"
  and explains what to do; the plugin and the skin are unaffected.

### Two tiers - ask first

| Tier | How | Result | Cost |
| --- | --- | --- | --- |
| **Universal** (default) | Official spritesheet interface only, no patching | Nine states animate; survives any Codex update | 4-8 frames per state, frame timing fixed by Codex |
| **Faithful** | Patch a copy of Codex so it plays the source GIFs directly | No transcoding, no scaling, no dropped frames (267 frames stay 267) | Tied to specific builds; a Codex update may need re-adapting |

Default to universal. Only go faithful when the user asks for source-GIF fidelity, and say upfront
what it costs. Both can coexist, and switching later is allowed.

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

### After a Codex update

The patch only knows the builds and SHA-256 hashes recorded in the source, so an official
update can invalidate it. That is not a bug, it is the cost of this approach. Do not guess -
get facts:

1. Read-only probe, changes nothing:

```powershell
node qa\patch-dry-run.mjs
```

   It finds the Store build itself and reports `verdict`: `patchable`, `already-patched`,
   `legacy` (an older patch the launcher upgrades in place), `unsupported` (unknown build) or
   `broken` (known build, transform fails). It also prints both bundles' `archivePath` and
   `sha256`.
2. On `unsupported` / `broken`: extract the new bundles (`qa\inspect-asar-targets.mjs`,
   `qa\read-asar-snippets.mjs`) and compare the anchors with `TARGET_PROFILES`. When the update
   only recompiled the renderer, adding one profile (renderer filename + baseline hash) and
   updating the state checks is often the whole job.
3. If it really cannot be adapted, tell the user the truth: the faithful tier is unavailable on
   that build for now, fall back to the universal tier or wait. **Never** relax hash checks,
   skip syntax validation, or claim success to make the install finish.

After adapting, verify `--action plan` -> `--action apply` -> `--action verify` all pass, and
that `--action restore` returns cleanly to `baseline`.

### Rules for changing this repository

The user may ask you to change code, not just install it. Every change must pass these checks;
do not commit without them:

```powershell
.\dist\studio\SkinStudio.exe --selftest      # studio layout self-test, expects failures=0
node --test qa\bundle-syntax.test.mjs        # patch replacement blocks must stay parseable
node --test qa\hot-reload.test.mjs           # hot-reload injection and query invalidation
$env:CODEX_SKIN_TEST_HOME = "qa/test-home"; node qa\core-check.mjs   # engine CRUD
```

After changing the plugin itself (`plugins\codex-skin-engine\`), bump the cachebuster and
reinstall, otherwise Codex keeps using the old copy:

```powershell
python C:\Users\yuan\.codex\skills\.system\plugin-creator\scripts\update_plugin_cachebuster.py .\plugins\codex-skin-engine
codex plugin add codex-skin-engine@personal
```

Two red lines: never move or rewrite the user's existing backup paths (everything under
`skin-engine\backups\` is their way back), and never turn the skin delete into a real delete -
it moves the skin to the backup folder on purpose.

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

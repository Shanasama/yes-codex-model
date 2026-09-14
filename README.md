# codex皮肤插件

把 WineFox（酒狐）皮肤装进 Codex，并让九种状态直接播放 GIF。

## 中文使用

先把下载的 ZIP 完整解压，然后双击根目录唯一的启动入口：

| 文件 | 用途 |
| --- | --- |
| `一键安装并启用 GIF.cmd` | 安装插件、完整 GIF 运行时并启动 Codex |

不需要完整流程时，`备用启动脚本` 文件夹里还保留了四个拆分入口：

| 文件 | 用途 |
| --- | --- |
| `备用启动脚本\安装到 Codex.cmd` | 只安装插件和酒狐皮肤 |
| `备用启动脚本\启动皮肤工坊.cmd` | 只打开本地皮肤编辑页面 |
| `备用启动脚本\安装可写 GIF 运行时.cmd` | 单独安装或刷新 GIF 运行时 |
| `备用启动脚本\启动可写 GIF 运行时.cmd` | 启动已经装好的 GIF 版 Codex |

启动器会自己查找当前目录、Node.js、Codex 和 Windows 用户目录，不绑定作者
电脑上的用户名或盘符。

## English usage

1. Download the repository or a Release ZIP and extract it to a normal writable
   folder.
2. Double-click the only launcher in the repository root:
   `一键安装并启用 GIF.cmd`. It installs the plugin, prepares direct GIF
   playback, and starts Codex. The first run copies about 1.8GB of the installed
   Codex app into a writable shadow runtime.
3. For individual actions, open the `备用启动脚本` folder. It contains separate
   launchers for plugin-only installation, the skin editor, shadow-runtime
   installation, and shadow-runtime startup.

All launchers detect the repository root, Node.js, Codex, and Windows user
directories automatically, so they do not depend on the original developer's
username or drive letter.

Requirements: Windows 10/11, Codex Desktop, Node.js 20 or newer, and the .NET 7
Desktop Runtime for the native Grilling window. The
launcher can also find Node.js bundled in a local Codex runtime. The full GIF
patch only supports the Codex bundle hashes listed in the source; an unknown
version is rejected without patching.

## 完整 GIF 是怎么做的

Windows Store 版 Codex 的安装目录不能直接写入，所以脚本会把当前 Codex
复制到 `%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes`，只修改
这个用户可写的副本。

第一次需要额外复制约 1.8GB。脚本不会修改 WindowsApps 的所有者或权限，
原来的 Store 版 Codex 仍然保留。

完整 GIF 补丁只认代码里记录过的 Codex 文件版本和 SHA-256。Codex 更新后
如果不匹配，脚本会直接拒绝安装，避免把新版程序改坏。

## 皮肤工坊

推荐使用原生窗口版 Grilling 皮肤工坊，不再打开网页：双击根目录的
`启动 Grilling 皮肤工坊.cmd`。首次运行会调用 .NET 7 SDK 构建一个轻量的
`dist\grilling\GrillingSkinStudio.exe`；之后直接启动原生窗口。它复用同一套
本地皮肤引擎 API，支持皮肤选择、GIF 替换、导入/导出、复制、元数据保存、应用
和完整动画运行时设置。

原来的 `备用启动脚本\启动皮肤工坊.cmd` 也已切换到原生窗口入口；如需调试网页
版，可直接运行 `start_studio.ps1`。

工坊地址是：

```text
http://127.0.0.1:43821
```

它只在本机运行，可以替换九种状态 GIF、导入和导出 `.codexskin`，以及应用
选中的皮肤。导入时会拦截脚本、可执行文件、危险路径和异常大的压缩包。

可选的 API 用量动画联动默认关闭，默认地址为 DeepSeek。Key 以明文保存在当前用户
目录的设置文件中，不会写进这个仓库；仅建议在个人测试环境使用。

## 测试

```powershell
node --test qa\bundle-syntax.test.mjs
$env:CODEX_SKIN_TEST_HOME = "qa/test-home"
node qa\core-check.mjs
$env:CODEX_SKIN_TEST_HOME = "qa/usage-test-home"
node qa\usage-check.mjs
```

想先检查环境但不执行安装，可以这样运行：

```powershell
$env:CODEX_SKIN_PLAN = "1"
& ".\一键安装并启用 GIF.cmd"
```

## 关于素材

程序代码使用 [MIT License](LICENSE)。

WineFox GIF 和 WebP 图集的原作者是酒石酸君，不属于本项目的 MIT 代码许可。
把带素材的仓库公开到 GitHub 前，最好先确认原作者是否允许公开再分发。程序
安装和打包时会原样复制 GIF，不会偷偷转码、缩放或删帧。

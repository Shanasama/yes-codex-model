// 只读探测：这份 app.asar 能不能打 GIF 直读补丁？全程不写任何文件。
// 用法：node qa/patch-dry-run.mjs [app.asar]
// 不传路径时自动在 WindowsApps 里找 Store 版 Codex。
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const patcher = path.join(here, "..", "plugins", "codex-skin-engine", "scripts", "patch_codex_gif_runtime.mjs");

function findStoreAsar() {
  if (process.platform !== "win32") return null;
  const viaPackage = storeAsarFromPackage();
  if (viaPackage) return viaPackage;
  const root = "C:\\Program Files\\WindowsApps";
  let entries = [];
  try { entries = fs.readdirSync(root); } catch { return null; }
  const candidates = entries
    .filter((name) => name.startsWith("OpenAI.Codex_"))
    .map((name) => path.join(root, name, "app", "resources", "app.asar"))
    .filter((file) => fs.existsSync(file))
    .sort();
  return candidates.at(-1) ?? null;
}

function run(action, extra = []) {
  const args = [patcher, "--action", action, "--asar", target, ...extra];
  try {
    return { ok: true, report: JSON.parse(execFileSync(process.execPath, args, { encoding: "utf8", windowsHide: true, timeout: 180000 })) };
  } catch (error) {
    const detail = String(error.stderr || error.stdout || error.message).trim().split(/\\r?\\n/).filter(Boolean);
    return { ok: false, error: detail.at(-1) ?? "补丁脚本执行失败" };
  }
}

const target = process.argv[2] ? path.resolve(process.argv[2]) : findStoreAsar();
if (!target) throw new Error("没找到 app.asar：请把路径作为参数传进来。");
if (!fs.existsSync(target)) throw new Error(`路径不存在：${target}`);

const verified = run("verify");
if (!verified.ok) {
  console.log(JSON.stringify({
    asar: target,
    supported: false,
    verdict: "unsupported",
    error: verified.error,
    next: "这个 Codex 版本的结构补丁脚本还不认识。要先读 TARGET_PROFILES 支持的版本，再按新版 bundle 重新适配；不要直接改，也不要声称支持。",
  }, null, 2));
  process.exit(0);
}

const info = verified.report;
const targets = Object.fromEntries(Object.entries(info.targets).map(([name, item]) => [name, { archivePath: item.archivePath, sha256: item.sha256, upToDate: item.upToDate }]));
const result = {
  asar: target,
  supported: info.state !== "unknown",
  state: info.state,
  runtimeProfile: info.runtimeProfile,
  targets,
};

if (info.state === "baseline") {
  const planned = run("plan");
  result.verdict = planned.ok ? "patchable" : "broken";
  result.plan = planned.ok ? planned.report.planned : null;
  result.next = planned.ok
    ? "可以打补丁：复制成影子运行时后按正常流程安装即可。"
    : `补丁脚本认这个版本，但改不动：${planned.error}`;
} else if (info.state === "gif-patched") {
  result.verdict = "already-patched";
  result.next = "已经打过补丁，直接用启动器打开就行。";
} else if (info.state === "gif-patched-legacy") {
  result.verdict = "legacy";
  result.next = "装的是旧版补丁，启动器会自动原地升级。";
} else {
  result.verdict = "unknown";
  result.next = "状态不明确（文件被改过或混用），先恢复兼容模式再重试。";
}

console.log(JSON.stringify(result, null, 2));


// 直接列 WindowsApps 会被拒（EPERM），先用包管理器问安装位置。
function storeAsarFromPackage() {
  const command = [
    "$p = Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | Select-Object -First 1;",
    "if (-not $p) { exit 1 };",
    "Join-Path $p.InstallLocation 'app\\resources\\app.asar'",
  ].join(" ");
  try {
    const value = execFileSync("powershell.exe", ["-NoLogo", "-NoProfile", "-Command", command], { encoding: "utf8", windowsHide: true, timeout: 30000 }).trim();
    return value && fs.existsSync(value) ? value : null;
  } catch { return null; }
}

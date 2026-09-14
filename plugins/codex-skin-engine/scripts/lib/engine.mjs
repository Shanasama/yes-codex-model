import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync, spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { inspectGif } from "./gif.mjs";
import { createZip, readZip } from "./zip.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const PLUGIN_ROOT = path.resolve(HERE, "..", "..");
const CODEX_ROOT = path.resolve(process.env.CODEX_SKIN_TEST_HOME || process.env.CODEX_HOME || path.join(os.homedir(), ".codex"));
export const DATA_ROOT = path.join(CODEX_ROOT, "skin-engine");
export const PETS_ROOT = path.join(CODEX_ROOT, "pets");
export const BUNDLED_ROOT = path.join(PLUGIN_ROOT, "presets");
export const USER_SKINS_ROOT = path.join(DATA_ROOT, "skins");
const CONFIG_PATH = path.join(DATA_ROOT, "config.json");
const PATCHER_PATH = path.join(PLUGIN_ROOT, "scripts", "patch_codex_gif_runtime.mjs");
const MAX_SKIN_FILES = 80;
const MAX_SKIN_BYTES = 512 * 1024 * 1024;
const BLOCKED_EXTENSIONS = new Set([".bat", ".cmd", ".com", ".dll", ".exe", ".js", ".mjs", ".msi", ".ps1", ".py", ".scr", ".vbs"]);

export const STATES = Object.freeze([
  { id: "idle", label: "待机", hint: "无任务时持续播放" },
  { id: "running-right", label: "向右移动", hint: "宠物向右移动" },
  { id: "running-left", label: "向左移动", hint: "宠物向左移动" },
  { id: "waving", label: "互动", hint: "招手或回应" },
  { id: "jumping", label: "跳跃", hint: "悬停交互" },
  { id: "failed", label: "失败", hint: "任务失败" },
  { id: "waiting", label: "等待", hint: "等待输入或授权" },
  { id: "running", label: "工作中", hint: "Codex 正在运行" },
  { id: "review", label: "审查", hint: "检查结果" }
]);

function ensureDataDirectories() {
  for (const directory of [DATA_ROOT, USER_SKINS_ROOT, path.join(DATA_ROOT, "backups"), path.join(DATA_ROOT, "exports"), path.join(DATA_ROOT, "staging")]) {
    fs.mkdirSync(directory, { recursive: true });
  }
}

function readJson(filePath, fallback = null) {
  try {
    return JSON.parse(fs.readFileSync(filePath, "utf8"));
  } catch (error) {
    if (fallback !== null && error.code === "ENOENT") return fallback;
    throw new Error(`JSON 读取失败 ${filePath}: ${error.message}`);
  }
}

function writeJsonAtomic(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const temporary = `${filePath}.new-${process.pid}`;
  fs.writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  fs.renameSync(temporary, filePath);
}

function safeRelative(value, label = "路径") {
  if (typeof value !== "string" || !value || value.includes("\0") || path.isAbsolute(value) || /^[A-Za-z]:/.test(value)) {
    throw new Error(`${label}无效`);
  }
  const normalized = value.replaceAll("\\", "/");
  const parts = normalized.split("/").filter(Boolean);
  if (!parts.length || parts.some((part) => part === "." || part === "..")) throw new Error(`${label}包含越界片段`);
  return parts.join("/");
}

function inside(root, relativePath) {
  const result = path.resolve(root, ...safeRelative(relativePath).split("/"));
  const relation = path.relative(path.resolve(root), result);
  if (!relation || relation.startsWith("..") || path.isAbsolute(relation)) {
    if (!relation) return result;
    throw new Error("路径超出皮肤目录");
  }
  return result;
}

function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  const descriptor = fs.openSync(filePath, "r");
  const buffer = Buffer.alloc(1024 * 1024);
  try {
    for (;;) {
      const count = fs.readSync(descriptor, buffer, 0, buffer.length, null);
      if (!count) break;
      hash.update(buffer.subarray(0, count));
    }
  } finally {
    fs.closeSync(descriptor);
  }
  return hash.digest("hex");
}

function slug(value) {
  const result = String(value || "").normalize("NFKD").replace(/[^a-zA-Z0-9]+/g, "-").replace(/^-+|-+$/g, "").toLowerCase();
  return result.slice(0, 64) || "custom-skin";
}

function timestamp() {
  const now = new Date();
  const pad = (number) => String(number).padStart(2, "0");
  return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
}

function listFiles(root) {
  const files = [];
  function visit(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const absolute = path.join(directory, entry.name);
      const relative = path.relative(root, absolute).replaceAll("\\", "/");
      if (entry.isSymbolicLink()) throw new Error(`皮肤不得包含符号链接：${relative}`);
      if (entry.isDirectory()) visit(absolute);
      else if (entry.isFile()) files.push({ absolute, relative, bytes: fs.statSync(absolute).size });
      else throw new Error(`不支持的文件类型：${relative}`);
    }
  }
  visit(root);
  if (files.length > MAX_SKIN_FILES) throw new Error(`皮肤文件数超过 ${MAX_SKIN_FILES}`);
  const total = files.reduce((sum, file) => sum + file.bytes, 0);
  if (total > MAX_SKIN_BYTES) throw new Error("皮肤总大小超过 512MB");
  return files;
}

function copyTree(source, destination) {
  fs.mkdirSync(destination, { recursive: true });
  for (const file of listFiles(source)) {
    const target = inside(destination, file.relative);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.copyFileSync(file.absolute, target);
    if (sha256File(file.absolute) !== sha256File(target)) throw new Error(`复制校验失败：${file.relative}`);
  }
}

function inspectFallback(filePath) {
  const buffer = fs.readFileSync(filePath);
  if (buffer.length < 16) throw new Error("回退图集文件过小");
  const png = buffer.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]));
  const webp = buffer.subarray(0, 4).toString("ascii") === "RIFF" && buffer.subarray(8, 12).toString("ascii") === "WEBP";
  if (!png && !webp) throw new Error("回退图集必须是 PNG 或 WebP");
  return { format: png ? "png" : "webp", bytes: buffer.length, sha256: sha256File(filePath) };
}

function stateFile(stateValue) {
  return typeof stateValue === "string" ? stateValue : stateValue?.file;
}

export function validateSkin(root, options = {}) {
  const skinPath = path.join(root, "skin.json");
  if (!fs.existsSync(skinPath)) throw new Error("皮肤缺少 skin.json");
  const manifest = readJson(skinPath);
  if (manifest.schemaVersion !== 1) throw new Error("仅支持 skin.json schemaVersion 1");
  if (!/^[a-z0-9][a-z0-9-]{0,63}$/.test(manifest.id || "")) throw new Error("皮肤 id 必须是小写英文、数字和连字符");
  if (typeof manifest.name !== "string" || !manifest.name.trim() || manifest.name.length > 80) throw new Error("皮肤名称无效");
  if (!manifest.states || typeof manifest.states !== "object") throw new Error("皮肤缺少 states");

  const states = {};
  for (const state of STATES) {
    const relative = safeRelative(stateFile(manifest.states[state.id]), `${state.label} GIF 路径`);
    if (path.extname(relative).toLowerCase() !== ".gif") throw new Error(`${state.label} 必须使用 GIF`);
    const absolute = inside(root, relative);
    const info = inspectGif(fs.readFileSync(absolute));
    const hash = sha256File(absolute);
    const expected = typeof manifest.states[state.id] === "object" ? manifest.states[state.id].sha256 : null;
    if (expected && expected !== hash) throw new Error(`${state.label} SHA-256 不匹配`);
    states[state.id] = { ...state, file: relative, absolute, sha256: hash, ...info };
  }

  const fallbackRelative = safeRelative(manifest.fallback || "spritesheet.webp", "回退图集路径");
  const fallbackAbsolute = inside(root, fallbackRelative);
  const fallback = { file: fallbackRelative, absolute: fallbackAbsolute, ...inspectFallback(fallbackAbsolute) };
  let thumbnail = null;
  if (manifest.thumbnail) {
    const relative = safeRelative(manifest.thumbnail, "缩略图路径");
    const absolute = inside(root, relative);
    if (!fs.existsSync(absolute)) throw new Error("缩略图不存在");
    thumbnail = { file: relative, absolute, bytes: fs.statSync(absolute).size, sha256: sha256File(absolute) };
  }

  const files = listFiles(root);
  for (const file of files) {
    if (BLOCKED_EXTENSIONS.has(path.extname(file.relative).toLowerCase())) throw new Error(`皮肤包只能包含数据文件：${file.relative}`);
  }
  return {
    id: manifest.id,
    name: manifest.name,
    description: String(manifest.description || ""),
    version: String(manifest.version || "1.0.0"),
    author: manifest.author || { name: "Unknown" },
    license: String(manifest.license || "Unspecified"),
    root: path.resolve(root),
    builtIn: Boolean(options.builtIn),
    manifest,
    fallback,
    thumbnail,
    states,
    files: files.length,
    bytes: files.reduce((sum, file) => sum + file.bytes, 0),
    valid: true
  };
}

function publicSkin(skin) {
  return {
    id: skin.id,
    name: skin.name,
    description: skin.description,
    version: skin.version,
    author: skin.author,
    license: skin.license,
    builtIn: skin.builtIn,
    bytes: skin.bytes,
    files: skin.files,
    thumbnail: skin.thumbnail?.file || null,
    fallback: { file: skin.fallback.file, format: skin.fallback.format, bytes: skin.fallback.bytes },
    states: Object.fromEntries(Object.entries(skin.states).map(([id, item]) => [id, {
      id,
      label: item.label,
      hint: item.hint,
      file: item.file,
      sha256: item.sha256,
      width: item.width,
      height: item.height,
      frames: item.frames,
      durationMs: item.durationMs,
      averageFrameMs: item.averageFrameMs,
      bytes: item.bytes,
      loopCount: item.loopCount
    }]))
  };
}

function rootsUnder(parent, builtIn) {
  if (!fs.existsSync(parent)) return [];
  return fs.readdirSync(parent, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => {
      try {
        return validateSkin(path.join(parent, entry.name), { builtIn });
      } catch (error) {
        return { id: entry.name, name: entry.name, root: path.join(parent, entry.name), builtIn, valid: false, error: error.message };
      }
    });
}

export function listSkins() {
  ensureDataDirectories();
  const merged = new Map();
  for (const skin of rootsUnder(BUNDLED_ROOT, true)) merged.set(skin.id, skin);
  for (const skin of rootsUnder(USER_SKINS_ROOT, false)) merged.set(skin.id, skin);
  return [...merged.values()].map((skin) => skin.valid ? publicSkin(skin) : skin).sort((a, b) => a.name.localeCompare(b.name, "zh-CN"));
}

export function getSkin(id) {
  ensureDataDirectories();
  const cleanId = slug(id);
  const userRoot = path.join(USER_SKINS_ROOT, cleanId);
  if (fs.existsSync(path.join(userRoot, "skin.json"))) return validateSkin(userRoot, { builtIn: false });
  const builtInRoot = path.join(BUNDLED_ROOT, cleanId);
  if (fs.existsSync(path.join(builtInRoot, "skin.json"))) return validateSkin(builtInRoot, { builtIn: true });
  throw new Error(`找不到皮肤：${id}`);
}

function ensureEditable(id) {
  const skin = getSkin(id);
  if (!skin.builtIn) return skin;
  const destination = path.join(USER_SKINS_ROOT, skin.id);
  if (!fs.existsSync(destination)) copyTree(skin.root, destination);
  const manifest = readJson(path.join(destination, "skin.json"));
  manifest.updatedAt = new Date().toISOString();
  writeJsonAtomic(path.join(destination, "skin.json"), manifest);
  return validateSkin(destination, { builtIn: false });
}

export function replaceState(id, stateId, buffer) {
  if (!STATES.some((state) => state.id === stateId)) throw new Error(`未知状态：${stateId}`);
  const gif = inspectGif(buffer);
  const skin = ensureEditable(id);
  const targetRelative = `animations/${stateId}.gif`;
  const target = inside(skin.root, targetRelative);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  const temporary = `${target}.new-${process.pid}`;
  fs.writeFileSync(temporary, buffer);
  fs.renameSync(temporary, target);
  const hash = sha256File(target);
  const manifestPath = path.join(skin.root, "skin.json");
  const manifest = readJson(manifestPath);
  manifest.states[stateId] = { file: targetRelative, sha256: hash };
  manifest.updatedAt = new Date().toISOString();
  writeJsonAtomic(manifestPath, manifest);
  const updated = validateSkin(skin.root, { builtIn: false });
  return { skin: publicSkin(updated), state: updated.states[stateId], gif };
}

function uniqueSkinId(base) {
  const initial = slug(base);
  let candidate = initial;
  let number = 2;
  while (fs.existsSync(path.join(USER_SKINS_ROOT, candidate)) || fs.existsSync(path.join(BUNDLED_ROOT, candidate))) candidate = `${initial.slice(0, 58)}-${number++}`;
  return candidate;
}

export function duplicateSkin(id, requestedName) {
  ensureDataDirectories();
  const source = getSkin(id);
  const name = String(requestedName || `${source.name} 副本`).trim().slice(0, 80);
  const newId = uniqueSkinId(name);
  const destination = path.join(USER_SKINS_ROOT, newId);
  copyTree(source.root, destination);
  const manifestPath = path.join(destination, "skin.json");
  const manifest = readJson(manifestPath);
  manifest.id = newId;
  manifest.name = name;
  manifest.version = "1.0.0";
  manifest.updatedAt = new Date().toISOString();
  writeJsonAtomic(manifestPath, manifest);
  return publicSkin(validateSkin(destination, { builtIn: false }));
}

export function updateSkin(id, changes) {
  const skin = ensureEditable(id);
  const manifestPath = path.join(skin.root, "skin.json");
  const manifest = readJson(manifestPath);
  if (changes.name !== undefined) {
    const name = String(changes.name).trim();
    if (!name || name.length > 80) throw new Error("皮肤名称无效");
    manifest.name = name;
  }
  if (changes.description !== undefined) manifest.description = String(changes.description).trim().slice(0, 500);
  if (changes.author !== undefined) manifest.author = { name: String(changes.author).trim().slice(0, 80) || "Unknown" };
  manifest.updatedAt = new Date().toISOString();
  writeJsonAtomic(manifestPath, manifest);
  return publicSkin(validateSkin(skin.root, { builtIn: false }));
}

export function importSkin(input) {
  ensureDataDirectories();
  const entries = readZip(input);
  if (!entries.some((entry) => entry.name === "skin.json")) throw new Error("皮肤包根目录缺少 skin.json");
  for (const entry of entries) {
    if (BLOCKED_EXTENSIONS.has(path.extname(entry.name).toLowerCase())) throw new Error(`皮肤包不得包含可执行文件：${entry.name}`);
  }
  const temporary = path.join(DATA_ROOT, "staging", `import-${crypto.randomUUID()}`);
  fs.mkdirSync(temporary, { recursive: true });
  try {
    for (const entry of entries) {
      const target = inside(temporary, entry.name);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, entry.data);
    }
    const checked = validateSkin(temporary);
    const newId = uniqueSkinId(checked.id);
    if (newId !== checked.id) {
      const manifestPath = path.join(temporary, "skin.json");
      const manifest = readJson(manifestPath);
      manifest.id = newId;
      manifest.name = `${manifest.name} (导入)`;
      writeJsonAtomic(manifestPath, manifest);
    }
    const destination = path.join(USER_SKINS_ROOT, newId);
    fs.renameSync(temporary, destination);
    return publicSkin(validateSkin(destination, { builtIn: false }));
  } catch (error) {
    fs.rmSync(temporary, { recursive: true, force: true });
    throw error;
  }
}

export function importSkinPath(inputPath) {
  const absolute = path.resolve(inputPath);
  if (!fs.existsSync(absolute)) throw new Error(`文件不存在：${absolute}`);
  return importSkin(fs.readFileSync(absolute));
}

export function exportSkin(id, destination = null) {
  ensureDataDirectories();
  const skin = getSkin(id);
  validateSkin(skin.root, { builtIn: skin.builtIn });
  const files = listFiles(skin.root).sort((a, b) => a.relative.localeCompare(b.relative));
  const output = path.resolve(destination || path.join(DATA_ROOT, "exports", `${skin.id}-${skin.version}.codexskin`));
  if (path.extname(output).toLowerCase() !== ".codexskin") throw new Error("导出文件必须使用 .codexskin 扩展名");
  createZip(files.map((file) => ({ name: file.relative, path: file.absolute })), output);
  return { path: output, bytes: fs.statSync(output).size, sha256: sha256File(output), files: files.length };
}

export function applySkin(id) {
  ensureDataDirectories();
  const skin = getSkin(id);
  const stage = path.join(DATA_ROOT, "staging", `apply-${skin.id}-${crypto.randomUUID()}`);
  const animations = path.join(stage, "animations");
  fs.mkdirSync(animations, { recursive: true });
  let backup = null;
  try {
    const fallbackName = `spritesheet.${skin.fallback.format}`;
    fs.copyFileSync(skin.fallback.absolute, path.join(stage, fallbackName));
    const animationPaths = {};
    const hashes = {};
    for (const state of STATES) {
      const source = skin.states[state.id];
      const fileName = `${state.id}.gif`;
      const target = path.join(animations, fileName);
      fs.copyFileSync(source.absolute, target);
      const targetHash = sha256File(target);
      if (targetHash !== source.sha256) throw new Error(`${state.label} 安装后 SHA-256 不匹配`);
      animationPaths[state.id] = `animations/${fileName}`;
      hashes[state.id] = targetHash;
    }
    if (sha256File(path.join(stage, fallbackName)) !== skin.fallback.sha256) throw new Error("回退图集安装后 SHA-256 不匹配");
    const petManifest = {
      id: skin.id,
      displayName: skin.name,
      description: skin.description,
      spriteVersionNumber: 2,
      spritesheetPath: fallbackName,
      animationPaths
    };
    writeJsonAtomic(path.join(stage, "pet.json"), petManifest);
    const target = path.join(PETS_ROOT, skin.id);
    fs.mkdirSync(PETS_ROOT, { recursive: true });
    if (fs.existsSync(target)) {
      backup = path.join(DATA_ROOT, "backups", "pets", skin.id, timestamp());
      fs.mkdirSync(path.dirname(backup), { recursive: true });
      fs.renameSync(target, backup);
    }
    try {
      fs.renameSync(stage, target);
    } catch (error) {
      if (backup && !fs.existsSync(target)) fs.renameSync(backup, target);
      throw error;
    }
    const receipt = {
      appliedAt: new Date().toISOString(),
      id: skin.id,
      name: skin.name,
      version: skin.version,
      target,
      backup,
      hashes,
      fallbackSha256: skin.fallback.sha256
    };
    writeJsonAtomic(path.join(DATA_ROOT, "last-apply.json"), receipt);
    return receipt;
  } catch (error) {
    fs.rmSync(stage, { recursive: true, force: true });
    throw error;
  }
}

function config() {
  ensureDataDirectories();
  return readJson(CONFIG_PATH, {});
}

function saveConfig(value) {
  writeJsonAtomic(CONFIG_PATH, value);
}

function currentChatGptExecutables() {
  if (process.platform !== "win32") return [];
  const command = "[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'ChatGPT.exe' -and $_.ExecutablePath } | Select-Object -ExpandProperty ExecutablePath -Unique";
  try {
    return execFileSync("powershell.exe", ["-NoProfile", "-Command", command], { encoding: "utf8", windowsHide: true, timeout: 5000 })
      .split(/\r?\n/).map((value) => value.trim()).filter(Boolean);
  } catch {
    return [];
  }
}

export function runtimeCandidates() {
  const values = [];
  for (const executable of currentChatGptExecutables()) values.push(path.join(path.dirname(executable), "resources", "app.asar"));
  if (process.env.CODEX_SKIN_ASAR) values.push(process.env.CODEX_SKIN_ASAR);
  const saved = config().runtimePath;
  if (saved) values.push(saved);
  return [...new Set(values.map((value) => path.resolve(value)))].filter((value) => fs.existsSync(value));
}

function runPatcher(action, asarPath, backupDir = null) {
  const args = [PATCHER_PATH, "--action", action, "--asar", path.resolve(asarPath)];
  if (backupDir) args.push("--backup-dir", backupDir);
  try {
    return JSON.parse(execFileSync(process.execPath, args, { encoding: "utf8", windowsHide: true, timeout: 120000 }));
  } catch (error) {
    const detail = String(error.stderr || error.stdout || error.message).trim();
    throw new Error(detail || `运行时${action}失败`);
  }
}

function runtimeBackupDir(asarPath) {
  return path.join(DATA_ROOT, "backups", "runtime", crypto.createHash("sha1").update(asarPath).digest("hex").slice(0, 12));
}

function readAsarHeader(asarPath) {
  const descriptor = fs.openSync(asarPath, "r");
  try {
    const prefix = Buffer.alloc(8);
    if (fs.readSync(descriptor, prefix, 0, 8, 0) !== 8) throw new Error(`无法读取 ASAR 头部：${asarPath}`);
    const headerSize = prefix.readUInt32LE(4);
    if (!headerSize) throw new Error(`无法读取 ASAR 头部：${asarPath}`);
    const header = Buffer.alloc(headerSize);
    let position = 0;
    while (position < headerSize) {
      const read = fs.readSync(descriptor, header, position, headerSize - position, 8 + position);
      if (!read) throw new Error(`无法读取 ASAR 头部：${asarPath}`);
      position += read;
    }
    return header;
  } finally {
    fs.closeSync(descriptor);
  }
}

function shadowSourceAsar(asarPath) {
  let current = path.dirname(path.resolve(asarPath));
  for (let depth = 0; depth < 4; depth += 1) {
    const metadataPath = path.join(current, "shadow-runtime.json");
    if (fs.existsSync(metadataPath)) {
      try {
        const metadata = JSON.parse(fs.readFileSync(metadataPath, "utf8"));
        if (metadata && typeof metadata.sourceAsar === "string" && fs.existsSync(metadata.sourceAsar)) return metadata.sourceAsar;
      } catch {
        return null;
      }
      return null;
    }
    current = path.dirname(current);
  }
  return null;
}

function ensureRuntimeHeaderBackup(asarPath, status) {
  const backupDir = runtimeBackupDir(asarPath);
  const headerBackup = path.join(backupDir, "header-baseline.pickle");
  if (fs.existsSync(headerBackup)) return backupDir;
  const sourceAsar = shadowSourceAsar(asarPath);
  if (!sourceAsar) return backupDir;
  if (fs.statSync(sourceAsar).size !== fs.statSync(asarPath).size) return backupDir;
  const header = readAsarHeader(sourceAsar);
  if (header.length !== status.headerSize) return backupDir;
  fs.mkdirSync(backupDir, { recursive: true });
  fs.writeFileSync(headerBackup, header);
  return backupDir;
}

export function runtimeStatus(requestedPath = null) {
  const candidates = requestedPath ? [path.resolve(requestedPath)] : runtimeCandidates();
  if (!candidates.length) return { state: "not-found", ok: false, message: "未发现可写的 Codex app.asar，可在设置中指定路径。" };
  const reports = candidates.map((asarPath) => {
    try {
      return { ...runPatcher("verify", asarPath), compatible: true };
    } catch (error) {
      return { asar: asarPath, state: "unsupported", ok: false, compatible: false, message: error.message };
    }
  });
  return reports.find((item) => item.state === "gif-patched")
    || reports.find((item) => item.state === "baseline")
    || reports.find((item) => item.state === "gif-patched-legacy")
    || reports[0];
}

export function setRuntimePath(asarPath) {
  const absolute = path.resolve(asarPath);
  if (!fs.existsSync(absolute) || path.basename(absolute).toLowerCase() !== "app.asar") throw new Error("请选择有效的 app.asar");
  const value = config();
  value.runtimePath = absolute;
  saveConfig(value);
  return runtimeStatus(absolute);
}

export function patchRuntime(requestedPath = null) {
  const status = runtimeStatus(requestedPath);
  if (!status.asar) throw new Error(status.message || "未发现运行时");
  if (status.state === "gif-patched") return { ...status, changed: false, restartRequired: false };
  if (status.state !== "baseline" && status.state !== "gif-patched-legacy") throw new Error(status.message || "当前 Codex 版本不受补丁支持");
  setRuntimePath(status.asar);
  runPatcher("plan", status.asar);
  const backupDir = status.state === "gif-patched-legacy"
    ? ensureRuntimeHeaderBackup(status.asar, status)
    : runtimeBackupDir(status.asar);
  const result = runPatcher("apply", status.asar, backupDir);
  return { ...result, backupDir, restartRequired: true };
}

export function restoreRuntime(requestedPath = null) {
  const status = runtimeStatus(requestedPath);
  if (!status.asar) throw new Error(status.message || "未发现运行时");
  if (status.state === "baseline") return { ...status, changed: false, restartRequired: false };
  if (status.state !== "gif-patched" && status.state !== "gif-patched-legacy") throw new Error(status.message || "运行时状态不明确，拒绝回滚");
  const backupDir = status.state === "gif-patched-legacy"
    ? ensureRuntimeHeaderBackup(status.asar, status)
    : runtimeBackupDir(status.asar);
  const result = runPatcher("restore", status.asar, backupDir);
  return { ...result, backupDir, restartRequired: true };
}

export function bootstrap() {
  const lastApply = readJson(path.join(DATA_ROOT, "last-apply.json"), {});
  return {
    version: "1.0.0",
    states: STATES,
    skins: listSkins(),
    runtime: runtimeStatus(),
    paths: { plugin: PLUGIN_ROOT, data: DATA_ROOT, pets: PETS_ROOT },
    lastApply
  };
}

export function skinAsset(id, relativePath) {
  const skin = getSkin(id);
  const absolute = inside(skin.root, relativePath);
  if (!fs.existsSync(absolute) || !fs.statSync(absolute).isFile()) throw new Error("素材不存在");
  return absolute;
}

export function studioExecutable() {
  return path.join(path.resolve(PLUGIN_ROOT, "..", ".."), "dist", "studio", "SkinStudio.exe");
}

export function openStudio() {
  const executable = studioExecutable();
  if (!fs.existsSync(executable)) {
    return {
      started: false,
      executable,
      message: "皮肤工坊还没编译。请先双击仓库根目录的 启动皮肤工坊.cmd，它会编译出一个独立窗口程序。"
    };
  }
  const child = spawn(executable, [], { detached: true, stdio: "ignore", cwd: path.dirname(executable) });
  child.unref();
  return { started: true, executable, message: "皮肤工坊窗口已打开" };
}

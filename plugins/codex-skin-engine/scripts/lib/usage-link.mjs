import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

export const USAGE_POLL_MS = 30_000;
export const DEFAULT_API_BASE_URL = "https://api.deepseek.com";

const USAGE_ENDPOINT_PATH = "/v1/organization/usage/completions";

const CODEX_ROOT = path.resolve(process.env.CODEX_SKIN_TEST_HOME || process.env.CODEX_HOME || path.join(os.homedir(), ".codex"));
const DATA_ROOT = path.join(CODEX_ROOT, "skin-engine");
export const USAGE_SETTINGS_PATH = path.join(DATA_ROOT, "usage-link.json");
export const USAGE_STATE_PATH = path.join(DATA_ROOT, "usage-state.json");
const USAGE_LOCK_PATH = path.join(DATA_ROOT, "usage-monitor.lock");
const SENSITIVITY_LIMITS = Object.freeze({ gentle: 20_000, balanced: 8_000, vivid: 3_000 });
const DEFAULT_SETTINGS = Object.freeze({
  enabled: false,
  speedEnabled: true,
  tintEnabled: true,
  sensitivity: "balanced",
  apiBaseUrl: DEFAULT_API_BASE_URL
});

let monitorTimer = null;
let ownsMonitorLock = false;

function normalizeApiBaseUrl(value) {
  const raw = String(value || DEFAULT_API_BASE_URL).trim() || DEFAULT_API_BASE_URL;
  let url;
  try {
    url = new URL(raw);
  } catch {
    throw new Error("API 地址格式不正确，请填写完整的 http:// 或 https:// 地址");
  }
  if (url.protocol !== "https:" && url.protocol !== "http:") {
    throw new Error("API 地址仅支持 http:// 或 https://");
  }
  if (url.username || url.password) throw new Error("API 地址不能包含账号或密码");
  url.hash = "";
  url.pathname = url.pathname.replace(/\/+$/, "");
  return url.toString().replace(/\/$/, "");
}

function storedApiBaseUrl(value) {
  try {
    return normalizeApiBaseUrl(value);
  } catch {
    return DEFAULT_API_BASE_URL;
  }
}

function usageEndpoint(apiBaseUrl) {
  const url = new URL(apiBaseUrl);
  const pathname = url.pathname.replace(/\/+$/, "");
  if (!/\/organization\/usage\/completions$/i.test(pathname)) {
    url.pathname = /\/v1$/i.test(pathname)
      ? `${pathname}/organization/usage/completions`
      : `${pathname}${USAGE_ENDPOINT_PATH}`;
  }
  url.search = "";
  url.hash = "";
  return url;
}

function ensureDataRoot() {
  fs.mkdirSync(DATA_ROOT, { recursive: true });
}

function readJson(filePath, fallback) {
  try {
    return JSON.parse(fs.readFileSync(filePath, "utf8"));
  } catch (error) {
    if (error.code === "ENOENT") return fallback;
    throw new Error(`设置文件读取失败：${error.message}`);
  }
}

function writeJsonAtomic(filePath, value) {
  ensureDataRoot();
  const temporary = `${filePath}.new-${process.pid}`;
  fs.writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  fs.renameSync(temporary, filePath);
}

function settingsRecord() {
  const stored = readJson(USAGE_SETTINGS_PATH, {});
  return {
    version: 3,
    revision: typeof stored.revision === "string" ? stored.revision : null,
    enabled: stored.enabled === true,
    speedEnabled: stored.speedEnabled !== false,
    tintEnabled: stored.tintEnabled !== false,
    sensitivity: SENSITIVITY_LIMITS[stored.sensitivity] ? stored.sensitivity : DEFAULT_SETTINGS.sensitivity,
    apiBaseUrl: storedApiBaseUrl(stored.apiBaseUrl),
    apiKey: typeof stored.apiKey === "string" && stored.apiKey ? stored.apiKey : null,
    keyHint: typeof stored.keyHint === "string" ? stored.keyHint : null,
    savedAt: typeof stored.savedAt === "string" ? stored.savedAt : null
  };
}

function publicSettings(record = settingsRecord()) {
  return {
    enabled: record.enabled,
    speedEnabled: record.speedEnabled,
    tintEnabled: record.tintEnabled,
    sensitivity: record.sensitivity,
    apiBaseUrl: record.apiBaseUrl,
    keyConfigured: Boolean(record.apiKey),
    keyHint: record.keyHint,
    savedAt: record.savedAt,
    pollSeconds: USAGE_POLL_MS / 1000,
    source: "compatible-usage-api"
  };
}

function baseState(record = settingsRecord()) {
  const needsKey = record.enabled && !record.apiKey;
  return {
    version: 1,
    settingsRevision: record.revision,
    enabled: record.enabled,
    status: record.enabled ? (needsKey ? "needs-key" : "waiting") : "disabled",
    tokensLast30s: 0,
    intensity: 0,
    speedMultiplier: 1,
    redPercent: 0,
    updatedAt: null,
    nextPollAt: record.enabled ? new Date(Date.now() + USAGE_POLL_MS).toISOString() : null,
    message: record.enabled
      ? (needsKey ? "请先保存 API Key" : "等待第一次用量统计")
      : "用量联动未开启",
    day: null,
    totalTokensToday: null
  };
}

function stateRecord(record = settingsRecord()) {
  const stored = readJson(USAGE_STATE_PATH, null);
  if (!stored || stored.settingsRevision !== record.revision || stored.enabled !== record.enabled) return baseState(record);
  return { ...baseState(record), ...stored };
}

function publicState(record = stateRecord()) {
  return {
    enabled: record.enabled === true,
    status: record.status || "disabled",
    tokensLast30s: Number.isFinite(record.tokensLast30s) ? Math.max(0, Math.round(record.tokensLast30s)) : 0,
    intensity: Number.isFinite(record.intensity) ? Math.max(0, Math.min(1, record.intensity)) : 0,
    speedMultiplier: Number.isFinite(record.speedMultiplier) ? Math.max(1, Math.min(1.5, record.speedMultiplier)) : 1,
    redPercent: Number.isFinite(record.redPercent) ? Math.max(0, Math.min(75, Math.round(record.redPercent))) : 0,
    updatedAt: record.updatedAt || null,
    nextPollAt: record.nextPollAt || null,
    message: record.message || ""
  };
}

function validateApiKey(value) {
  const key = String(value || "").trim();
  if (key.length < 20 || key.length > 512 || /\s/.test(key)) throw new Error("Key 格式不正确，请检查后重试");
  return key;
}

export function getUsageBundle() {
  const record = settingsRecord();
  return { settings: publicSettings(record), state: publicState(stateRecord(record)) };
}

export function saveUsageSettings(input = {}) {
  const current = settingsRecord();
  const next = {
    ...current,
    revision: crypto.randomUUID(),
    enabled: input.enabled === true,
    speedEnabled: input.speedEnabled !== false,
    tintEnabled: input.tintEnabled !== false,
    sensitivity: SENSITIVITY_LIMITS[input.sensitivity] ? input.sensitivity : DEFAULT_SETTINGS.sensitivity,
    apiBaseUrl: normalizeApiBaseUrl(input.apiBaseUrl),
    savedAt: new Date().toISOString()
  };
  const suppliedKey = typeof input.apiKey === "string" ? input.apiKey.trim() : "";
  if (suppliedKey) {
    const key = validateApiKey(suppliedKey);
    next.apiKey = key;
    next.keyHint = key.slice(-4);
  }
  if (next.enabled && !next.apiKey) throw new Error("开启用量联动前，请填写 API Key");
  writeJsonAtomic(USAGE_SETTINGS_PATH, next);
  const state = baseState(next);
  writeJsonAtomic(USAGE_STATE_PATH, state);
  return { settings: publicSettings(next), state: publicState(state) };
}

export function clearUsageKey() {
  const current = settingsRecord();
  const next = {
    ...current,
    revision: crypto.randomUUID(),
    enabled: false,
    apiKey: null,
    keyHint: null,
    savedAt: new Date().toISOString()
  };
  writeJsonAtomic(USAGE_SETTINGS_PATH, next);
  const state = baseState(next);
  writeJsonAtomic(USAGE_STATE_PATH, state);
  return { settings: publicSettings(next), state: publicState(state) };
}

function utcDayStartSeconds(now) {
  return Math.floor(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()) / 1000);
}

function usageTotal(payload) {
  if (!Array.isArray(payload?.data)) throw new Error("服务返回了无法识别的用量数据");
  return payload.data.reduce((bucketTotal, bucket) => bucketTotal + (Array.isArray(bucket?.results) ? bucket.results : [])
    .reduce((resultTotal, result) => resultTotal
      + (Number.isFinite(result?.input_tokens) ? result.input_tokens : 0)
      + (Number.isFinite(result?.output_tokens) ? result.output_tokens : 0), 0), 0);
}

function effectValues(tokens, settings) {
  const limit = SENSITIVITY_LIMITS[settings.sensitivity] || SENSITIVITY_LIMITS.balanced;
  const intensity = Math.max(0, Math.min(1, Math.sqrt(tokens / limit)));
  return {
    intensity: Number(intensity.toFixed(3)),
    speedMultiplier: settings.speedEnabled ? Number((1 + intensity * 0.5).toFixed(2)) : 1,
    redPercent: settings.tintEnabled ? Math.round(intensity * 75) : 0
  };
}

function errorMessage(error) {
  if (error?.status === 401 || error?.status === 403) return "Key 无法读取用量，请检查 Key 或服务权限";
  if (error?.status === 404) return "服务不支持兼容 Usage 接口，无法读取工作用量";
  if (error?.status === 429) return "服务暂时限制了查询频率，稍后会自动重试";
  if (error?.name === "AbortError" || error?.name === "TimeoutError") return "连接用量服务超时，稍后会自动重试";
  return error?.message || "用量查询失败，稍后会自动重试";
}

export async function pollUsageNow({ fetchImpl = globalThis.fetch, now = new Date() } = {}) {
  const settings = settingsRecord();
  if (!settings.enabled || !settings.apiKey) {
    const state = baseState(settings);
    if (fs.existsSync(USAGE_SETTINGS_PATH)) writeJsonAtomic(USAGE_STATE_PATH, state);
    return publicState(state);
  }

  const previous = stateRecord(settings);
  const day = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())).toISOString().slice(0, 10);
  try {
    if (typeof fetchImpl !== "function") throw new Error("当前 Node.js 版本不支持安全联网查询");
    const key = settings.apiKey;
    const url = usageEndpoint(settings.apiBaseUrl);
    url.searchParams.set("start_time", String(utcDayStartSeconds(now)));
    url.searchParams.set("end_time", String(Math.max(utcDayStartSeconds(now) + 1, Math.floor(now.getTime() / 1000))));
    url.searchParams.set("bucket_width", "1d");
    url.searchParams.set("limit", "1");
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 12_000);
    let response;
    try {
      response = await fetchImpl(url, {
        method: "GET",
        headers: { Authorization: `Bearer ${key}` },
        signal: controller.signal
      });
    } finally {
      clearTimeout(timeout);
    }
    if (!response?.ok) {
      const failure = new Error("用量查询失败");
      failure.status = response?.status;
      throw failure;
    }
    const totalTokensToday = usageTotal(await response.json());
    const canCompare = previous.day === day && Number.isFinite(previous.totalTokensToday);
    const tokensLast30s = canCompare ? Math.max(0, totalTokensToday - previous.totalTokensToday) : 0;
    const effects = effectValues(tokensLast30s, settings);
    const state = {
      version: 1,
      settingsRevision: settings.revision,
      enabled: true,
      status: canCompare ? (tokensLast30s > 0 ? "active" : "idle") : "waiting",
      tokensLast30s,
      ...effects,
      updatedAt: now.toISOString(),
      nextPollAt: new Date(now.getTime() + USAGE_POLL_MS).toISOString(),
      message: canCompare
        ? (tokensLast30s > 0 ? "正在根据最新用量调整工作动画" : "本次刷新没有新增用量")
        : "连接成功，等待下一次 30 秒统计",
      day,
      totalTokensToday
    };
    writeJsonAtomic(USAGE_STATE_PATH, state);
    return publicState(state);
  } catch (error) {
    const state = {
      ...baseState(settings),
      status: "error",
      updatedAt: now.toISOString(),
      nextPollAt: new Date(now.getTime() + USAGE_POLL_MS).toISOString(),
      message: errorMessage(error),
      day: previous.day || day,
      totalTokensToday: Number.isFinite(previous.totalTokensToday) ? previous.totalTokensToday : null
    };
    writeJsonAtomic(USAGE_STATE_PATH, state);
    return publicState(state);
  }
}

function processIsAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function acquireMonitorLock() {
  if (ownsMonitorLock) return true;
  ensureDataRoot();
  for (let attempt = 0; attempt < 2; attempt += 1) {
    try {
      const descriptor = fs.openSync(USAGE_LOCK_PATH, "wx");
      fs.writeFileSync(descriptor, JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString() }));
      fs.closeSync(descriptor);
      ownsMonitorLock = true;
      return true;
    } catch (error) {
      if (error.code !== "EEXIST") return false;
      let owner = {};
      try { owner = readJson(USAGE_LOCK_PATH, {}); } catch { owner = {}; }
      if (processIsAlive(Number(owner.pid))) return false;
      try { fs.rmSync(USAGE_LOCK_PATH, { force: true }); } catch { return false; }
    }
  }
  return false;
}

function releaseMonitorLock() {
  if (!ownsMonitorLock) return;
  try {
    const owner = readJson(USAGE_LOCK_PATH, {});
    if (Number(owner.pid) === process.pid) fs.rmSync(USAGE_LOCK_PATH, { force: true });
  } catch {
    // A stale lock is recovered by the next monitor process.
  }
  ownsMonitorLock = false;
}

export function startUsageMonitor() {
  if (monitorTimer) return;
  const tick = () => {
    if (acquireMonitorLock()) pollUsageNow().catch(() => {});
  };
  tick();
  monitorTimer = setInterval(tick, USAGE_POLL_MS);
  monitorTimer.unref?.();
  process.once("exit", releaseMonitorLock);
}

export function stopUsageMonitor() {
  if (monitorTimer) clearInterval(monitorTimer);
  monitorTimer = null;
  releaseMonitorLock();
}

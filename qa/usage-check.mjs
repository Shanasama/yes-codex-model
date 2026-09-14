import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  USAGE_SETTINGS_PATH,
  clearUsageKey,
  getUsageBundle,
  pollUsageNow,
  saveUsageSettings
} from "../plugins/codex-skin-engine/scripts/lib/usage-link.mjs";

const qaRoot = path.dirname(fileURLToPath(import.meta.url));
const testHome = path.resolve(process.env.CODEX_SKIN_TEST_HOME || "");
if (path.basename(testHome) !== "usage-test-home" || path.basename(path.dirname(testHome)) !== "qa") {
  throw new Error("usage-check 必须使用 qa/usage-test-home 作为 CODEX_SKIN_TEST_HOME");
}

const report = { checks: {} };
const testCredential = "unit-test-credential-not-a-real-api-key";

try {
  fs.rmSync(testHome, { recursive: true, force: true });
  const saved = saveUsageSettings({
    enabled: true,
    apiKey: testCredential,
    speedEnabled: true,
    tintEnabled: true,
    sensitivity: "balanced"
  });
  const storedText = fs.readFileSync(USAGE_SETTINGS_PATH, "utf8");
  const stored = JSON.parse(storedText);
  report.checks.secretStoredPlaintext = saved.settings.keyConfigured
    && saved.settings.keyHint === "-key"
    && storedText.includes(testCredential)
    && stored.apiKey === testCredential
    && !stored.encryptedKey;

  const requests = [];
  const responses = [
    { input_tokens: 1_000, output_tokens: 100 },
    { input_tokens: 3_000, output_tokens: 100 }
  ];
  const fetchImpl = async (url, options) => {
    requests.push({ url: String(url), authorization: options.headers.Authorization });
    const result = responses.shift();
    return { ok: true, status: 200, json: async () => ({ data: [{ results: [result] }] }) };
  };

  const first = await pollUsageNow({ fetchImpl, now: new Date("2026-09-12T12:00:00.000Z") });
  const second = await pollUsageNow({ fetchImpl, now: new Date("2026-09-12T12:00:30.000Z") });
  report.checks.officialUsageRequest = requests.length === 2
    && requests.every((request) => request.url.startsWith("https://api.deepseek.com/v1/organization/usage/completions?"))
    && requests.every((request) => request.authorization === `Bearer ${testCredential}`)
    && new URL(requests[0].url).searchParams.get("bucket_width") === "1d";
  report.checks.thirtySecondDelta = first.status === "waiting"
    && second.status === "active"
    && second.tokensLast30s === 2_000;
  report.checks.effectMapping = second.speedMultiplier === 1.25
    && second.redPercent === 38
    && second.intensity === 0.5;

  const customSaved = saveUsageSettings({
    enabled: true,
    apiBaseUrl: "https://api.deepseek.com/",
    apiKey: testCredential,
    speedEnabled: true,
    tintEnabled: true,
    sensitivity: "balanced"
  });
  let customRequestUrl = "";
  await pollUsageNow({
    fetchImpl: async (url) => {
      customRequestUrl = String(url);
      return { ok: true, status: 200, json: async () => ({ data: [{ results: [{ input_tokens: 1, output_tokens: 1 }] }] }) };
    },
    now: new Date("2026-09-12T12:00:00.000Z")
  });
  report.checks.customUsageBaseUrl = customSaved.settings.apiBaseUrl === "https://api.deepseek.com"
    && customRequestUrl.startsWith("https://api.deepseek.com/v1/organization/usage/completions?");

  const denied = await pollUsageNow({
    fetchImpl: async () => ({ ok: false, status: 403, json: async () => ({}) }),
    now: new Date("2026-09-12T12:01:00.000Z")
  });
  report.checks.permissionError = denied.status === "error" && denied.message.includes("检查 Key 或服务权限");

  const cleared = clearUsageKey();
  let disabledRequested = false;
  const disabled = await pollUsageNow({ fetchImpl: async () => { disabledRequested = true; throw new Error("should not run"); } });
  report.checks.clearKey = !cleared.settings.enabled && !cleared.settings.keyConfigured && getUsageBundle().settings.keyHint === null;
  report.checks.disabledMakesNoRequest = disabled.status === "disabled" && !disabledRequested;
  report.ok = Object.values(report.checks).every(Boolean);
} finally {
  fs.rmSync(testHome, { recursive: true, force: true });
}

fs.writeFileSync(path.join(qaRoot, "usage-report.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
if (!report.ok) process.exitCode = 1;

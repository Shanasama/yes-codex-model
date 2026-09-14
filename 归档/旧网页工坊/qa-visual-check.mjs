import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");
const qaRoot = path.dirname(fileURLToPath(import.meta.url));
const errors = [];
const programFilesX86 = process.env["ProgramFiles(x86)"];
const programFiles = process.env.ProgramFiles;
const browserExecutable = [
  process.env.PLAYWRIGHT_BROWSER,
  programFilesX86 && path.join(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
  programFiles && path.join(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
  chromium.executablePath()
].find((candidate) => candidate && fs.existsSync(candidate));
if (!browserExecutable) throw new Error("No Chromium or Edge executable is available");
const browser = await chromium.launch({ headless: true, executablePath: browserExecutable });

async function inspect(viewport, outputName) {
  const page = await browser.newPage({ viewport, deviceScaleFactor: 1 });
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(`console: ${message.text()}`);
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  await page.goto("http://127.0.0.1:43821", { waitUntil: "networkidle" });
  await page.waitForSelector("#state-grid .state-card");
  await page.waitForFunction(() => [...document.images].every((image) => image.complete && image.naturalWidth > 0));
  const facts = await page.evaluate(() => {
    const cards = [...document.querySelectorAll("#state-grid .state-card")];
    const buttons = [...document.querySelectorAll("button, label.button")];
    const clippedButtons = buttons.filter((button) => button.scrollWidth > button.clientWidth + 1 || button.scrollHeight > button.clientHeight + 1)
      .map((button) => button.textContent.trim());
    const advanced = document.querySelector("#runtime-advanced");
    const runtimePath = document.querySelector("#runtime-path");
    const usagePanel = document.querySelector(".usage-section");
    const runtimeCopy = document.querySelector("#animation-mode-section")?.innerText || "";
    const usageCopy = usagePanel?.textContent || "";
    return {
      title: document.title,
      cards: cards.length,
      visibleReplaceButtons: cards.filter((card) => card.querySelector("[data-replace]")?.getBoundingClientRect().width > 0).length,
      brokenImages: [...document.images].filter((image) => !image.complete || !image.naturalWidth).length,
      gifElements: [...document.images].filter((image) => new URL(image.src).pathname.endsWith(".gif")).length,
      horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
      clippedButtons,
      advancedCollapsed: advanced instanceof HTMLDetailsElement && !advanced.open,
      runtimePathHidden: runtimePath?.getBoundingClientRect().width === 0,
      plainRuntimeCopy: runtimeCopy.includes("完整动画") && !runtimeCopy.includes("app.asar") && !runtimeCopy.includes("SHA-256") && !runtimeCopy.includes("底层桥接"),
      usageCollapsed: usagePanel instanceof HTMLDetailsElement && !usagePanel.open,
      usageControlsPresent: Boolean(document.querySelector("#usage-enabled") && document.querySelector("#usage-key") && document.querySelector("#usage-save")),
      usageKeyNotExposed: document.querySelector("#usage-key")?.value === "",
      plainUsageCopy: usageCopy.includes("每 30 秒") && !usageCopy.includes("/v1/organization/usage"),
      viewport: { width: document.documentElement.clientWidth, height: window.innerHeight },
      document: { width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight }
    };
  });
  await page.screenshot({ path: path.join(qaRoot, outputName), fullPage: true });
  await page.close();
  return facts;
}

const desktop = await inspect({ width: 1440, height: 900 }, "ui-desktop.png");
const animationPage = await browser.newPage({ viewport: { width: 900, height: 700 } });
await animationPage.goto("http://127.0.0.1:43821", { waitUntil: "networkidle" });
await animationPage.waitForFunction(() => document.querySelector("#main-preview")?.complete);
const first = await animationPage.locator("#main-preview").screenshot();
await animationPage.waitForTimeout(450);
const second = await animationPage.locator("#main-preview").screenshot();
const animationMoves = crypto.createHash("sha256").update(first).digest("hex") !== crypto.createHash("sha256").update(second).digest("hex");
await animationPage.close();
const linkedPage = await browser.newPage({ viewport: { width: 900, height: 700 } });
await linkedPage.route("**/api/usage", (route) => route.fulfill({
  status: 200,
  contentType: "application/json",
  body: JSON.stringify({
    ok: true,
    data: {
      settings: { enabled: true, speedEnabled: true, tintEnabled: true, sensitivity: "balanced", keyConfigured: true, keyHint: "7890", pollSeconds: 30 },
      state: { enabled: true, status: "active", tokensLast30s: 6000, intensity: .75, speedMultiplier: 1.38, redPercent: 56, message: "正在根据最新用量调整工作动画" }
    }
  })
}));
await linkedPage.goto("http://127.0.0.1:43821", { waitUntil: "networkidle" });
await linkedPage.locator('[data-state="running"]').click();
const usageTintPreview = await linkedPage.locator("#main-preview").evaluate((image) => image.dataset.usageLevel === "3" && getComputedStyle(image).filter !== "none");
await linkedPage.close();
const mobile = await inspect({ width: 390, height: 844 }, "ui-mobile.png");
await browser.close();

const report = {
  ok: errors.length === 0
    && desktop.cards === 9
    && mobile.cards === 9
    && desktop.visibleReplaceButtons === 9
    && mobile.visibleReplaceButtons === 9
    && !desktop.horizontalOverflow
    && !mobile.horizontalOverflow
    && desktop.brokenImages === 0
    && mobile.brokenImages === 0
    && desktop.gifElements === 1
    && mobile.gifElements === 1
    && desktop.clippedButtons.length === 0
    && mobile.clippedButtons.length === 0
    && desktop.advancedCollapsed
    && mobile.advancedCollapsed
    && desktop.runtimePathHidden
    && mobile.runtimePathHidden
    && desktop.plainRuntimeCopy
    && mobile.plainRuntimeCopy
    && desktop.usageCollapsed
    && mobile.usageCollapsed
    && desktop.usageControlsPresent
    && mobile.usageControlsPresent
    && desktop.usageKeyNotExposed
    && mobile.usageKeyNotExposed
    && desktop.plainUsageCopy
    && mobile.plainUsageCopy
    && usageTintPreview
    && animationMoves,
  animationMoves,
  usageTintPreview,
  desktop,
  mobile,
  errors
};
fs.writeFileSync(path.join(qaRoot, "ui-report.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
if (!report.ok) process.exitCode = 1;

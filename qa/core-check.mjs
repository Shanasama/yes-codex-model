import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  STATES,
  applySkin,
  deleteSkin,
  exportSkin,
  getSkin,
  importSkin,
  listSkins,
  replaceState
} from "../plugins/codex-skin-engine/scripts/lib/engine.mjs";
import { createZip } from "../plugins/codex-skin-engine/scripts/lib/zip.mjs";

const qaRoot = path.dirname(fileURLToPath(import.meta.url));
const testHome = path.resolve(process.env.CODEX_SKIN_TEST_HOME || "");
if (path.basename(testHome) !== "test-home" || path.basename(path.dirname(testHome)) !== "qa") {
  throw new Error("core-check 必须使用 qa/test-home 作为 CODEX_SKIN_TEST_HOME");
}

function hash(filePath) {
  return crypto.createHash("sha256").update(fs.readFileSync(filePath)).digest("hex");
}

const report = { checks: {} };
const exportPath = path.join(qaRoot, "test-export.codexskin");
const blockedPath = path.join(qaRoot, "test-blocked.codexskin");

try {
  const skins = listSkins();
  report.checks.bundledSkin = skins.length === 1 && skins[0].id === "winefox-pixel-classic";

  const source = getSkin("winefox-pixel-classic");
  report.checks.thumbnailUsesIdle = source.thumbnail?.file === source.states.idle.file;
  const exported = exportSkin(source.id, exportPath);
  report.checks.export = exported.files === 11 && exported.bytes > 30 * 1024 * 1024;

  const imported = importSkin(fs.readFileSync(exportPath));
  report.checks.import = imported.id === "winefox-pixel-classic-2" && Object.keys(imported.states).length === 9;

  const runningSource = fs.readFileSync(source.states.running.absolute);
  const replaced = replaceState(imported.id, "running", runningSource);
  report.checks.replaceState = replaced.state.frames === 36 && replaced.state.sha256 === source.states.running.sha256;

  const receipt = applySkin(source.id);
  const pet = JSON.parse(fs.readFileSync(path.join(receipt.target, "pet.json"), "utf8"));
  report.checks.apply = pet.spriteVersionNumber === 2 && STATES.every((state) => pet.animationPaths[state.id]);
  report.checks.installHashes = STATES.every((state) => hash(path.join(receipt.target, "animations", `${state.id}.gif`)) === source.states[state.id].sha256);

  let invalidGifRejected = false;
  try { replaceState(imported.id, "idle", Buffer.from("not-a-gif")); } catch { invalidGifRejected = true; }
  report.checks.invalidGifRejected = invalidGifRejected;

  const skinJson = fs.readFileSync(path.join(source.root, "skin.json"));
  createZip([
    { name: "skin.json", data: skinJson },
    { name: "payload.js", data: Buffer.from("throw new Error('blocked')") }
  ], blockedPath);
  let executableRejected = false;
  try { importSkin(fs.readFileSync(blockedPath)); } catch (error) { executableRejected = /可执行文件/.test(error.message); }
  report.checks.executableRejected = executableRejected;

  let traversalRejected = false;
  try { createZip([{ name: "../escape.txt", data: Buffer.from("x") }], blockedPath); } catch (error) { traversalRejected = /危险路径/.test(error.message); }
  report.checks.traversalRejected = traversalRejected;

  const removed = deleteSkin(imported.id);
  report.checks.deleteSkin = !fs.existsSync(path.join(testHome, "skin-engine", "skins", imported.id))
    && fs.existsSync(path.join(removed.removedTo, "skin.json"))
    && listSkins().every((skin) => skin.id !== imported.id);

  let builtInProtected = false;
  try { deleteSkin("winefox-pixel-classic"); } catch (error) { builtInProtected = /自带皮肤/.test(error.message); }
  report.checks.builtInProtected = builtInProtected;

  report.ok = Object.values(report.checks).every(Boolean);
} finally {
  for (const file of [exportPath, blockedPath]) fs.rmSync(file, { force: true });
  fs.rmSync(testHome, { recursive: true, force: true });
}

fs.writeFileSync(path.join(qaRoot, "core-report.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
if (!report.ok) process.exitCode = 1;

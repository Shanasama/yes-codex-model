import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { execFile } from "node:child_process";
import {
  PLUGIN_ROOT,
  applySkin,
  bootstrap,
  duplicateSkin,
  exportSkin,
  importSkin,
  patchRuntime,
  replaceState,
  restoreRuntime,
  runtimeStatus,
  setRuntimePath,
  skinAsset,
  updateSkin
} from "./lib/engine.mjs";
import {
  clearUsageKey,
  getUsageBundle,
  pollUsageNow,
  saveUsageSettings,
  startUsageMonitor
} from "./lib/usage-link.mjs";

const HOST = "127.0.0.1";
const portIndex = process.argv.indexOf("--port");
const PORT = portIndex >= 0 ? Number(process.argv[portIndex + 1]) : 43821;
const SHOULD_OPEN = process.argv.includes("--open");
const WEB_ROOT = path.join(PLUGIN_ROOT, "web");
const MAX_BODY = 128 * 1024 * 1024;

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".gif": "image/gif",
  ".png": "image/png",
  ".webp": "image/webp",
  ".codexskin": "application/zip"
};

function headers(extra = {}) {
  return {
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "no-referrer",
    "Content-Security-Policy": "default-src 'self'; img-src 'self' data: blob:; style-src 'self'; script-src 'self'; connect-src 'self'",
    ...extra
  };
}

function sendJson(response, status, value) {
  response.writeHead(status, headers({ "Content-Type": MIME[".json"] }));
  response.end(JSON.stringify(value));
}

function sendError(response, error, status = 400) {
  sendJson(response, status, { ok: false, error: error.message || String(error) });
}

function readBody(request, limit = MAX_BODY) {
  return new Promise((resolve, reject) => {
    const parts = [];
    let bytes = 0;
    request.on("data", (part) => {
      bytes += part.length;
      if (bytes > limit) {
        reject(new Error("上传内容超过 128MB"));
        request.destroy();
        return;
      }
      parts.push(part);
    });
    request.on("end", () => resolve(Buffer.concat(parts)));
    request.on("error", reject);
  });
}

async function readJsonBody(request) {
  const buffer = await readBody(request, 1024 * 1024);
  if (!buffer.length) return {};
  return JSON.parse(buffer.toString("utf8"));
}

function sendFile(response, filePath, downloadName = null) {
  const extension = path.extname(filePath).toLowerCase();
  const stat = fs.statSync(filePath);
  const extra = {
    "Content-Type": MIME[extension] || "application/octet-stream",
    "Content-Length": stat.size
  };
  if (downloadName) extra["Content-Disposition"] = `attachment; filename*=UTF-8''${encodeURIComponent(downloadName)}`;
  response.writeHead(200, headers(extra));
  fs.createReadStream(filePath).pipe(response);
}

function openBrowser() {
  const url = `http://${HOST}:${PORT}`;
  if (process.platform === "win32") {
    execFile("powershell.exe", ["-NoProfile", "-Command", `Start-Process '${url}'`], { windowsHide: true });
  }
}

async function handleApi(request, response, url) {
  if (request.method === "GET" && url.pathname === "/api/health") return sendJson(response, 200, { ok: true, service: "codex-skin-engine", version: "1.0.0" });
  if (request.method === "GET" && url.pathname === "/api/bootstrap") return sendJson(response, 200, { ok: true, data: bootstrap() });
  if (request.method === "GET" && url.pathname === "/api/runtime") return sendJson(response, 200, { ok: true, data: runtimeStatus(url.searchParams.get("path")) });
  if (request.method === "GET" && url.pathname === "/api/usage") return sendJson(response, 200, { ok: true, data: getUsageBundle() });

  if (request.method === "POST" && url.pathname === "/api/usage/settings") {
    const saved = saveUsageSettings(await readJsonBody(request));
    const state = saved.settings.enabled ? await pollUsageNow() : saved.state;
    return sendJson(response, 200, { ok: true, data: { settings: saved.settings, state } });
  }
  if (request.method === "POST" && url.pathname === "/api/usage/refresh") {
    const state = await pollUsageNow();
    return sendJson(response, 200, { ok: true, data: { ...getUsageBundle(), state } });
  }
  if (request.method === "DELETE" && url.pathname === "/api/usage/key") {
    return sendJson(response, 200, { ok: true, data: clearUsageKey() });
  }

  if (request.method === "GET" && url.pathname.startsWith("/api/asset/")) {
    const parts = url.pathname.slice("/api/asset/".length).split("/").map(decodeURIComponent);
    const id = parts.shift();
    return sendFile(response, skinAsset(id, parts.join("/")));
  }

  if (request.method === "GET" && url.pathname === "/api/export") {
    const id = url.searchParams.get("id");
    const result = exportSkin(id);
    return sendFile(response, result.path, path.basename(result.path));
  }

  if (request.method === "POST" && url.pathname === "/api/import") {
    const skin = importSkin(await readBody(request));
    return sendJson(response, 201, { ok: true, data: skin });
  }

  if (request.method === "POST" && url.pathname === "/api/apply") {
    const body = await readJsonBody(request);
    return sendJson(response, 200, { ok: true, data: applySkin(body.id) });
  }

  if (request.method === "POST" && url.pathname === "/api/runtime/path") {
    const body = await readJsonBody(request);
    return sendJson(response, 200, { ok: true, data: setRuntimePath(body.path) });
  }
  if (request.method === "POST" && url.pathname === "/api/runtime/patch") {
    const body = await readJsonBody(request);
    return sendJson(response, 200, { ok: true, data: patchRuntime(body.path || null) });
  }
  if (request.method === "POST" && url.pathname === "/api/runtime/restore") {
    const body = await readJsonBody(request);
    return sendJson(response, 200, { ok: true, data: restoreRuntime(body.path || null) });
  }

  const stateMatch = url.pathname.match(/^\/api\/skin\/([^/]+)\/state\/([^/]+)$/);
  if (request.method === "POST" && stateMatch) {
    const result = replaceState(decodeURIComponent(stateMatch[1]), decodeURIComponent(stateMatch[2]), await readBody(request));
    return sendJson(response, 200, { ok: true, data: result.skin });
  }

  const duplicateMatch = url.pathname.match(/^\/api\/skin\/([^/]+)\/duplicate$/);
  if (request.method === "POST" && duplicateMatch) {
    const body = await readJsonBody(request);
    return sendJson(response, 201, { ok: true, data: duplicateSkin(decodeURIComponent(duplicateMatch[1]), body.name) });
  }

  const skinMatch = url.pathname.match(/^\/api\/skin\/([^/]+)$/);
  if (request.method === "PATCH" && skinMatch) {
    const body = await readJsonBody(request);
    return sendJson(response, 200, { ok: true, data: updateSkin(decodeURIComponent(skinMatch[1]), body) });
  }
  sendJson(response, 404, { ok: false, error: "API 不存在" });
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url, `http://${HOST}:${PORT}`);
    if (url.pathname.startsWith("/api/")) return await handleApi(request, response, url);
    if (url.pathname === "/favicon.ico") {
      response.writeHead(204, headers());
      return response.end();
    }
    const requested = url.pathname === "/" ? "index.html" : decodeURIComponent(url.pathname.slice(1));
    if (requested.includes("..") || requested.includes("\\")) return sendError(response, new Error("路径无效"), 403);
    const filePath = path.resolve(WEB_ROOT, requested);
    if (!filePath.startsWith(path.resolve(WEB_ROOT) + path.sep) && filePath !== path.join(WEB_ROOT, "index.html")) return sendError(response, new Error("路径无效"), 403);
    if (!fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) return sendError(response, new Error("页面不存在"), 404);
    sendFile(response, filePath);
  } catch (error) {
    sendError(response, error);
  }
});

startUsageMonitor();

server.once("error", (error) => {
  if (error.code === "EADDRINUSE" && SHOULD_OPEN) {
    openBrowser();
    process.exit(0);
  }
  console.error(error);
  process.exit(1);
});

server.listen(PORT, HOST, () => {
  console.log(`Codex Skin Engine: http://${HOST}:${PORT}`);
  if (SHOULD_OPEN) openBrowser();
});

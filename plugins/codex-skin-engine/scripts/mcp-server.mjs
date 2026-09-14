import fs from "node:fs";
import readline from "node:readline";
import {
  applySkin,
  bootstrap,
  exportSkin,
  importSkinPath,
  listSkins,
  openStudio,
  patchRuntime,
  replaceState,
  restoreRuntime,
  runtimeStatus
} from "./lib/engine.mjs";
import { getUsageBundle, startUsageMonitor } from "./lib/usage-link.mjs";

const TOOLS = [
  {
    name: "open_studio",
    description: "启动并打开 Codex Skin Engine 本地皮肤工坊。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false }
  },
  {
    name: "skin_status",
    description: "读取皮肤库、安装位置与 GIF 运行时桥接状态。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false }
  },
  {
    name: "list_skins",
    description: "列出已安装和插件内置的 Codex 宠物皮肤。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false }
  },
  {
    name: "import_skin",
    description: "从本地 .codexskin 文件导入经过安全校验的皮肤。",
    inputSchema: {
      type: "object",
      properties: { path: { type: "string", description: ".codexskin 的绝对路径" } },
      required: ["path"],
      additionalProperties: false
    }
  },
  {
    name: "export_skin",
    description: "把皮肤导出为可分享的 .codexskin 数据包。",
    inputSchema: {
      type: "object",
      properties: {
        skin_id: { type: "string" },
        destination: { type: "string", description: "可选的目标绝对路径" }
      },
      required: ["skin_id"],
      additionalProperties: false
    }
  },
  {
    name: "replace_state_gif",
    description: "用本地原始 GIF 替换指定皮肤状态；不缩放、不转码、不丢帧。",
    inputSchema: {
      type: "object",
      properties: {
        skin_id: { type: "string" },
        state: { type: "string", enum: ["idle", "running-right", "running-left", "waving", "jumping", "failed", "waiting", "running", "review"] },
        gif_path: { type: "string", description: "GIF 的绝对路径" }
      },
      required: ["skin_id", "state", "gif_path"],
      additionalProperties: false
    }
  },
  {
    name: "apply_skin",
    description: "校验 SHA-256 后安装皮肤到 Codex pets 目录，并保留旧版本备份。",
    inputSchema: {
      type: "object",
      properties: { skin_id: { type: "string" } },
      required: ["skin_id"],
      additionalProperties: false
    }
  },
  {
    name: "runtime_bridge",
    description: "检查、安装或回滚原始 GIF 运行时桥接。安装和回滚后需要重启 Codex。",
    inputSchema: {
      type: "object",
      properties: {
        action: { type: "string", enum: ["status", "install", "restore"] },
        asar_path: { type: "string", description: "可选 app.asar 绝对路径" }
      },
      required: ["action"],
      additionalProperties: false
    }
  },
  {
    name: "usage_link_status",
    description: "读取可选的 OpenAI API 用量动画联动状态；不会返回已保存的 Key。",
    inputSchema: { type: "object", properties: {}, additionalProperties: false }
  }
];

function textResult(value) {
  return {
    content: [{ type: "text", text: JSON.stringify(value, null, 2) }],
    structuredContent: value
  };
}

function callTool(name, args) {
  switch (name) {
    case "open_studio": return openStudio();
    case "skin_status": return bootstrap();
    case "list_skins": return { skins: listSkins() };
    case "import_skin": return importSkinPath(args.path);
    case "export_skin": return exportSkin(args.skin_id, args.destination || null);
    case "replace_state_gif": return replaceState(args.skin_id, args.state, fs.readFileSync(args.gif_path)).skin;
    case "apply_skin": return applySkin(args.skin_id);
    case "runtime_bridge":
      if (args.action === "status") return runtimeStatus(args.asar_path || null);
      if (args.action === "install") return patchRuntime(args.asar_path || null);
      return restoreRuntime(args.asar_path || null);
    case "usage_link_status": return getUsageBundle();
    default: throw new Error(`未知工具：${name}`);
  }
}

function reply(message) {
  process.stdout.write(`${JSON.stringify(message)}\n`);
}

const input = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
startUsageMonitor();
input.on("line", (line) => {
  if (!line.trim()) return;
  let request;
  try {
    request = JSON.parse(line);
    if (request.method === "notifications/initialized" || request.method === "notifications/cancelled") return;
    if (request.method === "initialize") {
      return reply({
        jsonrpc: "2.0",
        id: request.id,
        result: {
          protocolVersion: request.params?.protocolVersion || "2025-06-18",
          capabilities: { tools: { listChanged: false } },
          serverInfo: { name: "codex-skin-engine", version: "1.0.0" }
        }
      });
    }
    if (request.method === "ping") return reply({ jsonrpc: "2.0", id: request.id, result: {} });
    if (request.method === "tools/list") return reply({ jsonrpc: "2.0", id: request.id, result: { tools: TOOLS } });
    if (request.method === "tools/call") {
      const result = callTool(request.params?.name, request.params?.arguments || {});
      return reply({ jsonrpc: "2.0", id: request.id, result: textResult(result) });
    }
    if (request.id !== undefined) reply({ jsonrpc: "2.0", id: request.id, error: { code: -32601, message: "Method not found" } });
  } catch (error) {
    if (request?.id !== undefined) reply({ jsonrpc: "2.0", id: request.id, result: { content: [{ type: "text", text: error.message }], isError: true } });
  }
});

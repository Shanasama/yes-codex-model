import fs from "node:fs";
import {
  applySkin,
  bootstrap,
  exportSkin,
  getSkin,
  importSkinPath,
  listSkins,
  openStudio,
  patchRuntime,
  restoreRuntime,
  runtimeStatus,
  validateSkin
} from "./lib/engine.mjs";

const [command = "help", ...args] = process.argv.slice(2);
let result;
switch (command) {
  case "status": result = bootstrap(); break;
  case "list": result = listSkins(); break;
  case "validate":
    if (!args[0]) throw new Error("validate 需要皮肤目录");
    result = validateSkin(fs.realpathSync(args[0]));
    break;
  case "import": result = importSkinPath(args[0]); break;
  case "export": result = exportSkin(args[0], args[1] || null); break;
  case "apply": result = applySkin(args[0]); break;
  case "runtime-status": result = runtimeStatus(args[0] || null); break;
  case "runtime-patch": result = patchRuntime(args[0] || null); break;
  case "runtime-restore": result = restoreRuntime(args[0] || null); break;
  case "open": result = openStudio(); break;
  default:
    result = {
      usage: [
        "node scripts/cli.mjs status",
        "node scripts/cli.mjs list",
        "node scripts/cli.mjs validate <skin-directory>",
        "node scripts/cli.mjs import <file.codexskin>",
        "node scripts/cli.mjs export <skin-id> [output.codexskin]",
        "node scripts/cli.mjs apply <skin-id>",
        "node scripts/cli.mjs runtime-status [app.asar]",
        "node scripts/cli.mjs runtime-patch [app.asar]",
        "node scripts/cli.mjs runtime-restore [app.asar]",
        "node scripts/cli.mjs open"
      ]
    };
}
console.log(JSON.stringify(result, (key, value) => key === "absolute" || key === "root" ? undefined : value, 2));

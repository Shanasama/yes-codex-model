import { spawnSync } from "node:child_process";

// Parse the complete ESM source without executing it or resolving its imports.
export function checkModuleSyntax(source, filename) {
  const result = spawnSync(process.execPath, ["--check", "--input-type=module"], {
    input: source,
    encoding: "utf8",
    windowsHide: true,
    timeout: 30000,
    maxBuffer: 32 * 1024 * 1024,
  });
  if (result.error) throw new Error(`Module syntax check failed (${filename}): ${result.error.message}`);
  if (result.status !== 0) {
    const lines = String(result.stderr || "").split(/\r?\n/);
    const diagnostic = lines.find((line) => line.startsWith("SyntaxError:"))
      || `Node syntax check exited with status ${result.status}`;
    const location = lines.find((line) => /^\[stdin\]:\d+/.test(line)) || "";
    throw new SyntaxError(`${filename}${location.replace("[stdin]", "")}: ${diagnostic.slice(0, 500)}`);
  }
}

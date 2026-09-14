import assert from "node:assert/strict";
import test from "node:test";
import { checkModuleSyntax } from "../plugins/codex-skin-engine/scripts/lib/bundle-syntax.mjs";

test("validates complete minified ESM without resolving imports or executing source", () => {
  const source = 'import x from "missing-module";const text="import;export";export{ x };export default import.meta.url;await Promise.resolve();throw new Error("must not execute");';
  assert.doesNotThrow(() => checkModuleSyntax(source, "renderer.js"));
});

test("rejects malformed code and exports with concise diagnostics", () => {
  for (const source of [
    'import x from "missing-module";const broken=;',
    'const x=1;export { missing };',
    `const large=${JSON.stringify("x".repeat(100000))};const broken=;`,
  ]) {
    assert.throws(() => checkModuleSyntax(source, "renderer.js"), (error) => {
      assert.equal(error.name, "SyntaxError");
      assert.match(error.message, /renderer\.js.*SyntaxError:/);
      assert.ok(error.message.length < 600);
      return true;
    });
  }
});

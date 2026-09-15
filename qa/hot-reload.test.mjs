import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { createRequire } from "node:module";
import {
  HOT_RELOAD_MARKER,
  HOT_RELOAD_MESSAGE_TYPE,
  PETS_QUERY_KEY,
  VIEW_MESSAGE_CHANNEL,
  mainHotReload,
  rendererHotReload,
  resolveQueryClientGetter,
  withQueryClientCapture,
} from "../plugins/codex-skin-engine/scripts/lib/hot-reload.mjs";

const nodeRequire = createRequire(import.meta.url);
const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));


test("两段注入代码都带上热更新标记，而且不依赖 import", () => {
  for (const block of [mainHotReload(), rendererHotReload()]) {
    assert.ok(block.includes(HOT_RELOAD_MARKER));
    assert.ok(!/^\s*import\s/m.test(block));
  }
});


test("渲染层只在收到自家消息时让宠物查询失效", () => {
  const listeners = [];
  const invalidated = [];
  const fakeWindow = {
    addEventListener: (type, handler) => listeners.push(handler),
    __CODEX_SKIN_QUERY_CLIENT__: { invalidateQueries: (options) => invalidated.push(options) },
  };
  new Function("window", `${rendererHotReload()}\n`)(fakeWindow);
  const dispatch = (data) => {
    for (const handler of listeners) handler({ data });
  };
  dispatch({ type: "some-unrelated-message" });
  dispatch(null);
  assert.deepEqual(invalidated, []);
  dispatch({ type: HOT_RELOAD_MESSAGE_TYPE });
  assert.deepEqual(invalidated, [{ queryKey: [PETS_QUERY_KEY] }]);
});


test("主进程只在宠物文件真的变了以后才通知渲染层", async () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), "codex-skin-hot-"));
  const petDir = path.join(home, "pets", "demo");
  fs.mkdirSync(petDir, { recursive: true });
  fs.writeFileSync(path.join(petDir, "pet.json"), "{}");
  const sent = [];
  const electron = {
    webContents: {
      getAllWebContents: () => [{
        isDestroyed: () => false,
        send: (channel, message) => sent.push({ channel, message }),
      }],
    },
  };
  const harness = new Function("require", "jA", "process", `${mainHotReload()}\n`);
  harness((name) => (name === "electron" ? electron : nodeRequire(name)), () => home, process);
  try {
    await delay(1400);
    assert.deepEqual(sent, [], "文件没动就不该通知");
    fs.writeFileSync(path.join(petDir, "idle.gif"), "GIF89a-fake");
    await delay(2200);
    assert.equal(sent.length, 1);
    assert.equal(sent[0].channel, VIEW_MESSAGE_CHANNEL);
    assert.equal(sent[0].message.type, HOT_RELOAD_MESSAGE_TYPE);
  } finally {
    fs.rmSync(home, { recursive: true, force: true });
  }
});


test("能从压缩后的 bundle 里认出 queryClient 取用函数", () => {
  const sample = 'const x=1,_m=e=>{let t=ywt.useContext(xwt);if(e)return e;if(!t)throw Error(`No QueryClient set, use QueryClientProvider to set one`);return t},y=2;';
  assert.equal(resolveQueryClientGetter(sample), "_m");
  assert.equal(resolveQueryClientGetter("这里没有特征串"), null);
  assert.equal(
    withQueryClientCapture('ruo=({a:e})=>{"use forget";let x=1}', "_m"),
    'ruo=({a:e})=>{"use forget";try{window.__CODEX_SKIN_QUERY_CLIENT__=_m()}catch{}let x=1}',
  );
  assert.equal(withQueryClientCapture("ruo=()=>{}", null), "ruo=()=>{}");
});

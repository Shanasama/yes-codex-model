// 热更新注入代码：主进程盯住 pets 目录，渲染层收到通知后让宠物查询重新读盘。
// 这两段代码会被 patch_codex_gif_runtime.mjs 拼进 Codex 自己的 bundle 里，
// 所以只能用最普通的老式写法，不能 import 任何东西。
export const GIF_MARKER = "__CODEX_SKIN_GIF_RUNTIME_V1__";
export const HOT_RELOAD_MARKER = "__CODEX_SKIN_HOT_RELOAD_V1__";
export const PETS_QUERY_KEY = "custom-avatars";
export const VIEW_MESSAGE_CHANNEL = "codex_desktop:message-for-view";
export const HOT_RELOAD_MESSAGE_TYPE = "codex-skin-pets-changed";


export function mainHotReload() {
  return `
const ${HOT_RELOAD_MARKER}=1;
(()=>{try{
const fsHot=require("node:fs"),osHot=require("node:os"),pathHot=require("node:path");
let homeHot=null;try{homeHot=jA({})}catch{}
homeHot=homeHot??process.env.CODEX_HOME??pathHot.join(osHot.homedir(),".codex");
const petsHot=pathHot.join(homeHot,"pets");
const fingerprintHot=()=>{let names;try{names=fsHot.readdirSync(petsHot,{withFileTypes:true})}catch{return null}
const parts=[];for(const entry of names){if(!entry.isDirectory())continue;let files;try{files=fsHot.readdirSync(pathHot.join(petsHot,entry.name),{withFileTypes:true})}catch{files=[]}
for(const file of files){try{const info=fsHot.statSync(pathHot.join(petsHot,entry.name,file.name));parts.push(entry.name+"/"+file.name+":"+info.size+":"+info.mtimeMs)}catch{}}}
return parts.sort().join("|")};
const notifyHot=()=>{try{const{webContents}=require("electron");for(const wc of webContents.getAllWebContents()){try{if(!wc.isDestroyed())wc.send("${VIEW_MESSAGE_CHANNEL}",{type:"${HOT_RELOAD_MESSAGE_TYPE}"})}catch{}}}catch{}};
let seenHot=fingerprintHot(),timerHot=null;
const tickHot=()=>{const next=fingerprintHot();if(next==null||next===seenHot)return;seenHot=next;if(timerHot!=null)clearTimeout(timerHot);timerHot=setTimeout(()=>{timerHot=null;const settled=fingerprintHot();if(settled!=null)seenHot=settled;notifyHot()},500)};
const intervalHot=setInterval(tickHot,1000);
if(intervalHot.unref)intervalHot.unref();
}catch{}})();
`;
}


export function rendererHotReload() {
  return `
const ${HOT_RELOAD_MARKER}=1;
(()=>{try{
if(typeof window=="undefined"||window.__CODEX_SKIN_PETS_RELOAD_V1__)return;
window.__CODEX_SKIN_PETS_RELOAD_V1__=true;
const keyHot=["${PETS_QUERY_KEY}"];
window.addEventListener("message",event=>{const dataHot=event==null?null:event.data;
if(dataHot==null||typeof dataHot!="object"||dataHot.type!="${HOT_RELOAD_MESSAGE_TYPE}")return;
const clientHot=window.__CODEX_SKIN_QUERY_CLIENT__;
try{if(clientHot!=null)clientHot.invalidateQueries({queryKey:keyHot})}catch{}
});
}catch{}})();
`;
}


// 宠物列表/单个宠物资产都用这个 queryClient 失效；名字是打包后的短名，只能从特征串反推。
export function resolveQueryClientGetter(text) {
  const marker = "No QueryClient set, use QueryClientProvider to set one";
  const index = text.indexOf(marker);
  if (index < 0) return null;
  const before = text.slice(Math.max(0, index - 400), index);
  const match = /([A-Za-z_$][\w$]*)\s*=\s*[A-Za-z_$][\w$]*\s*=>\s*\{[^{}]*$/.exec(before);
  return match == null ? null : match[1];
}


// 在宠物组件里顺手把 queryClient 存到 window 上，热更新通知来了直接用它刷新。
export function withQueryClientCapture(component, getter) {
  const anchor = '{"use forget";';
  const index = component.indexOf(anchor);
  if (getter == null || index < 0) return component;
  return component.slice(0, index + anchor.length)
    + `try{window.__CODEX_SKIN_QUERY_CLIENT__=${getter}()}catch{}`
    + component.slice(index + anchor.length);
}


export function hotReloadReport(info) {
  const text = info.targets.renderer.buffer.toString("utf8");
  return {
    queryClientGetter: resolveQueryClientGetter(text),
    queryKeyPresent: text.includes("`" + PETS_QUERY_KEY + "`"),
  };
}

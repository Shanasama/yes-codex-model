import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { checkModuleSyntax } from "./lib/bundle-syntax.mjs";
import { GIF_MARKER, HOT_RELOAD_MARKER, PETS_QUERY_KEY, hotReloadReport, mainHotReload, rendererHotReload, resolveQueryClientGetter, withQueryClientCapture } from "./lib/hot-reload.mjs";


const TARGET_PROFILES = [
  {
    id: "codex-legacy-b6lqg3ek",
    main: {
      archivePath: ".vite/build/src-B6LqG3ek.js",
      supportedHashes: new Set([
        "9a1d9737c526cbba0e18e5ffc3b9371ef055318e7eabb7584b082fbcdb24472e",
      ]),
      legacyPatchHashes: new Set([
        "4993b5105f34fab0dd295d68d77ae6e6b3386b49d559f7cd1555de7d5e14cf0a",
      ]),
    },
    renderer: {
      archivePath: "webview/assets/app-initial-f094ef01c64d.js",
      supportedHashes: new Set([
        "364622097d1440b55bcd85b1068245a773f5821f4fd6c1e2de73e5789487c75a",
        "f3ba80e4afd393fadef600787da8e6cef71948a0a53373faf98c43a2c2d66047",
      ]),
      legacyPatchHashes: new Set([
        "42ac2c1e577f113bca0e3db10cc07235c1c80dbc7e5993744f1fb883b6d3561f",
      ]),
    },
  },
  {
    id: "codex-26.908.4834",
    main: {
      archivePath: ".vite/build/src-CCXHtyvY.js",
      supportedHashes: new Set([
        "a42da38cbb14b28399f1d54fcf453bffc5e9802663e7e098f187c8378f4c7a40",
      ]),
      legacyPatchHashes: new Set([
        "2daa8f4a8202952070f1f0f3e67b11693e2047698ceecb913ea52bf9a2e18642",
        "1e24affe40d3fcb3fb4cd98c09da087520ba877f1686afddbf074ea438537a4a",
      ]),
    },
    renderer: {
      archivePath: "webview/assets/app-initial-d9bed9d614d8.js",
      supportedHashes: new Set([
        "7c3a89e7e224f76031b45a88f72af8cd60f0c3d47aac9ca34b2c70e11dfe9867",
      ]),
      legacyPatchHashes: new Set([
        "214040bedf8e467e4dca5aaea62ba355cdb98c25c03d07bc12b8d0c001d268bd",
        "86a09422e21dfafbc220bfd4dc4441a7c0e0859b31c87403d60df788909f6a12",
      ]),
    },
  },
  {
    id: "codex-26.908.9136",
    main: {
      archivePath: ".vite/build/src-CCXHtyvY.js",
      supportedHashes: new Set([
        "a42da38cbb14b28399f1d54fcf453bffc5e9802663e7e098f187c8378f4c7a40",
      ]),
      legacyPatchHashes: new Set([
        "1e24affe40d3fcb3fb4cd98c09da087520ba877f1686afddbf074ea438537a4a",
      ]),
    },
    renderer: {
      archivePath: "webview/assets/app-initial-bcc2ff475eb6.js",
      supportedHashes: new Set([
        "3c15444f96a8d48844258618fe0d4278409e626f0ee563a77d2c669ec669c510",
      ]),
      legacyPatchHashes: new Set([]),
    },
  },
];


// 主进程 bundle 在 26.908.4834 与 26.908.9136 两版之间没变，只有渲染层换了实现。
const MODERN_PROFILE_IDS = new Set(["codex-26.908.4834", "codex-26.908.9136"]);

const MAIN_LOADER = `const __CODEX_SKIN_GIF_RUNTIME_V1__=1;
async function e0(e,t,n,r,i){let a=t0(e,n,r);if(a==null)return null;let o=e.join(a,i);try{let n=JSON.parse(await X.readFile(o,t)),i=J1.safeParse(n);if(!i.success)return null;let s=t0(e,a,i.data.spritesheetPath);if(s==null)return null;let c=await X.readFileBase64(s,t),l=typeof c=="string"?c:c.toString("base64"),u=Z1(Buffer.from(l,"base64"),i.data.spriteVersionNumber),d=Object.fromEntries(await Promise.all(Object.entries(n.animationPaths??{}).map(async([n,r])=>{let i=t0(e,a,r);if(i==null)return[n,null];let o=await X.readFileBase64(i,t),s=typeof o=="string"?o:o.toString("base64"),c=Buffer.from(s,"base64");return[n,c.length>=10&&c.subarray(0,3).toString("ascii")==="GIF"?"data:image/gif;base64,"+s:null]})));return u==null?null:{directoryPath:a,id:"custom:"+r,displayName:i.data.displayName??i.data.id??r,description:i.data.description,spriteVersionNumber:i.data.spriteVersionNumber,spritesheetDataUrl:u.spritesheetDataUrl,animationDataUrls:d}}catch{return null}}`;

const RENDERER_SOURCE = `const __CODEX_SKIN_GIF_RUNTIME_V1__=1;
function eer(e){return e.spritesheetUrl==null?{assetRef:e.assetRef}:{petId:e.displayName,spriteRowCount:Y9n(e.spriteVersionNumber),spritesheetUrl:e.spritesheetUrl,animationUrls:e.animationUrls}}`;

const RENDERER_COMPONENT = `wer=({assetMap:e,className:t,lookFrame:n,respondToHover:r=!1,source:i,state:a="idle"})=>{"use forget";let[o,s]=(0,Ser.useState)(!1),c=(0,Ser.useRef)(null),l=i.animationUrls?.[a],u=r&&o&&!l?"jumping":a,d=i.assetRef==null?i.spriteRowCount:IN.rows,f=i.animationUrls?.[u]??null,h=f;return(0,Ser.useEffect)(()=>{let e=c.current;if(e==null)return;if(h!=null){e.style.backgroundPosition="center bottom";return}if(n!=null){e.style.backgroundPosition=fer(n,d);return}let t=uer(u,BLe()||i.petId?.includes("WineFox")),r=t.frames,i=0,a=null;if(e.style.backgroundPosition=fer(der(r,i),d),r.length===1)return;let o=()=>{a=window.setTimeout(()=>{let n=i+1;if(n>=r.length){if(t.loopStartIndex!=null){i=t.loopStartIndex,e.style.backgroundPosition=fer(der(r,i),d),o();return}a=null;return}i=n,e.style.backgroundPosition=fer(der(r,i),d),o()},der(r,i).frameDurationMs)};return o(),()=>{a!=null&&window.clearTimeout(a)}},[u,n,d,h]),(0,Cer.jsx)("div",{ref:c,className:K(ber.Root,t),"data-codex-pet-asset-ref":i.assetRef,"data-codex-pet-id":i.assetRef??i.petId,"data-codex-pet-state":u,"data-codex-skin-usage-level":p?Math.ceil((m.intensity||0)*4):0,onPointerEnter:()=>{r&&s(!0)},onPointerLeave:()=>{r&&s(!1)},style:{backgroundImage:"url("+(h??i.spritesheetUrl??e[i.assetRef])+")",backgroundPosition:h!=null?"center bottom":void 0,backgroundRepeat:h!=null?"no-repeat":void 0,backgroundSize:h!=null?"contain":d==null?void 0:IN.columns*100+"% "+d*100+"%"},"aria-hidden":"true"})}}));`;

const MAIN_LOADER_26_908 = MAIN_LOADER.slice(0, MAIN_LOADER.indexOf("async function e0("))
  + `async function p2(e,t,n,r,i){let a=m2(e,n,r);if(a==null)return null;let o=e.join(a,i);try{let n=JSON.parse(await X.readFile(o,t)),i=s2.safeParse(n);if(!i.success)return null;let s=m2(e,a,i.data.spritesheetPath);if(s==null)return null;let c=await X.readFileBase64(s,t),l=typeof c=="string"?c:c.toString("base64"),u=u2(Buffer.from(l,"base64"),i.data.spriteVersionNumber),d=Object.fromEntries(await Promise.all(Object.entries(n.animationPaths??{}).map(async([n,r])=>{let i=m2(e,a,r);if(i==null)return[n,null];let o=await X.readFileBase64(i,t),s=typeof o=="string"?o:o.toString("base64"),c=Buffer.from(s,"base64");return[n,c.length>=10&&c.subarray(0,3).toString("ascii")=="GIF"?"data:image/gif;base64,"+s:null]})));return u==null?null:{directoryPath:a,id:"custom:"+r,displayName:i.data.displayName??i.data.id??r,description:i.data.description,spriteVersionNumber:i.data.spriteVersionNumber,spritesheetDataUrl:u.spritesheetDataUrl,animationDataUrls:d}}catch{return null}}`;

const RENDERER_SOURCE_26_908 = RENDERER_SOURCE.slice(0, RENDERER_SOURCE.indexOf("function eer(e)"))
  + `function Alo(e){return e.spritesheetUrl==null?{assetRef:e.assetRef}:{petId:e.id,spriteRowCount:Tlo(e.spriteVersionNumber),spritesheetUrl:e.spritesheetUrl,animationUrls:e.animationUrls}}`;

const RENDERER_COMPONENT_26_908 = `ruo=({assetMap:e,className:t,lookFrame:n,respondToHover:r=!1,source:i,state:a="idle"})=>{"use forget";let[o,s]=(0,tuo.useState)(!1),c=(0,tuo.useRef)(null),l=AXe(),u=i.animationUrls?.[a],d=r&&o&&!u?"jumping":a,p=i.assetRef==null?i.spriteRowCount:r4.rows,h=i.animationUrls?.[d]??null,f=h;return(0,tuo.useEffect)(()=>{let e=c.current;if(e==null)return;if(f!=null){e.style.backgroundPosition="center bottom";return}if(n!=null){e.style.backgroundPosition=Glo(n,p);return}let t=Ulo(d,l),r=t.frames,i=0,a=null;if(e.style.backgroundPosition=Glo(Wlo(r,i),p),r.length===1)return;let o=()=>{a=window.setTimeout(()=>{let n=i+1;if(n>=r.length){if(t.loopStartIndex!=null){i=t.loopStartIndex,e.style.backgroundPosition=Glo(Wlo(r,i),p),o();return}a=null;return}i=n,e.style.backgroundPosition=Glo(Wlo(r,i),p),o()},Wlo(r,i).frameDurationMs)};return o(),()=>{a!=null&&window.clearTimeout(a)}},[d,n,l,p,f]),(0,nuo.jsx)("div",{ref:c,className:S($lo.Root,t),"data-codex-pet-asset-ref":i.assetRef,"data-codex-pet-id":i.assetRef??i.petId,"data-codex-pet-state":d,onPointerEnter:()=>{r&&s(!0)},onPointerLeave:()=>{r&&s(!1)},style:{backgroundImage:"url("+(f??i.spritesheetUrl??e[i.assetRef])+")",backgroundPosition:f!=null?"center bottom":void 0,backgroundRepeat:f!=null?"no-repeat":void 0,backgroundSize:f!=null?"contain":p==null?void 0:r4.columns*100+"% "+p*100+"%"},"aria-hidden":"true"})}}));`;


function stripMarkerDeclaration(text, marker) {
  return text
    .replaceAll(`const ${marker}=1;\n`, "")
    .replaceAll(`const ${marker}=1;`, "");
}


function sha256(buffer) {
  return crypto.createHash("sha256").update(buffer).digest("hex");
}


function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  const fd = fs.openSync(filePath, "r");
  const buffer = Buffer.alloc(4 * 1024 * 1024);
  try {
    for (;;) {
      const count = fs.readSync(fd, buffer, 0, buffer.length, null);
      if (count === 0) break;
      hash.update(buffer.subarray(0, count));
    }
  } finally {
    fs.closeSync(fd);
  }
  return hash.digest("hex");
}


function readArchive(asarPath) {
  const fd = fs.openSync(asarPath, "r");
  try {
    const sizePickle = Buffer.alloc(8);
    fs.readSync(fd, sizePickle, 0, sizePickle.length, 0);
    const headerSize = sizePickle.readUInt32LE(4);
    const headerPickle = Buffer.alloc(headerSize);
    fs.readSync(fd, headerPickle, 0, headerPickle.length, 8);
    const jsonSize = headerPickle.readUInt32LE(4);
    const jsonText = headerPickle.subarray(8, 8 + jsonSize).toString("utf8");
    return {
      header: JSON.parse(jsonText),
      headerPickle,
      headerSize,
      baseOffset: 8 + headerSize,
    };
  } finally {
    fs.closeSync(fd);
  }
}


function findEntry(header, archivePath) {
  let entry = header;
  for (const part of archivePath.split("/")) {
    entry = entry.files?.[part];
    if (entry == null) throw new Error(`ASAR entry not found: ${archivePath}`);
  }
  return entry;
}

function hasEntry(header, archivePath) {
  try {
    findEntry(header, archivePath);
    return true;
  } catch {
    return false;
  }
}


function overlayPath(asarPath, archivePath) {
  return path.join(`${asarPath}.unpacked`, ...archivePath.split("/"));
}


function readEntry(asarPath, archive, archivePath) {
  const entry = findEntry(archive.header, archivePath);
  if (entry.unpacked === true) {
    const externalPath = overlayPath(asarPath, archivePath);
    return { buffer: fs.readFileSync(externalPath), entry, externalPath };
  }
  const size = Number(entry.size);
  const offset = archive.baseOffset + Number(entry.offset);
  const buffer = Buffer.alloc(size);
  const fd = fs.openSync(asarPath, "r");
  try {
    const count = fs.readSync(fd, buffer, 0, size, offset);
    if (count !== size) throw new Error(`Short ASAR entry read: ${archivePath}`);
  } finally {
    fs.closeSync(fd);
  }
  return { buffer, entry, externalPath: null };
}


function blockHashes(buffer, blockSize) {
  const blocks = [];
  for (let offset = 0; offset < buffer.length; offset += blockSize) {
    blocks.push(sha256(buffer.subarray(offset, Math.min(offset + blockSize, buffer.length))));
  }
  return blocks;
}


function integrityFor(buffer, blockSize = 4194304) {
  return {
    algorithm: "SHA256",
    hash: sha256(buffer),
    blockSize,
    blocks: blockHashes(buffer, blockSize),
  };
}


function countOccurrences(text, needle) {
  let count = 0;
  let offset = 0;
  for (;;) {
    const index = text.indexOf(needle, offset);
    if (index < 0) return count;
    count += 1;
    offset = index + needle.length;
  }
}


function replaceOnce(text, from, to) {
  const count = countOccurrences(text, from);
  if (count !== 1) throw new Error(`Expected one marker, found ${count}: ${from.slice(0, 120)}`);
  return text.replace(from, to);
}


function replaceSection(text, startMarker, endMarker, replacement) {
  const start = text.indexOf(startMarker);
  if (start < 0 || text.indexOf(startMarker, start + startMarker.length) >= 0) {
    throw new Error(`Expected one section start: ${startMarker}`);
  }
  const end = text.indexOf(endMarker, start + startMarker.length);
  if (end < 0) throw new Error(`Section end not found: ${endMarker}`);
  return text.slice(0, start) + replacement + text.slice(end);
}


function stripLegacyUsage(text, marker) {
  const start = text.indexOf(marker);
  if (start < 0) return text;
  const tail = "\n})();\n";
  const end = text.indexOf(tail, start + marker.length);
  if (end < 0) throw new Error(`旧用量代码结构无法识别：${marker}`);
  return text.slice(0, start) + text.slice(end + tail.length);
}


function transformMain(buffer, profile) {
  let text = buffer.toString("utf8");
  if (Buffer.byteLength(text, "utf8") !== buffer.length) throw new Error("Main bundle is not UTF-8");
  if (isCurrentPatch(buffer)) return buffer;
  text = stripLegacyUsage(text, "const __CODEX_SKIN_USAGE_SYNC_V1__=(()=>{");
  text = stripMarkerDeclaration(stripMarkerDeclaration(text, GIF_MARKER), HOT_RELOAD_MARKER);
  const modern = MODERN_PROFILE_IDS.has(profile.id);
  text = replaceSection(
    text,
    modern ? "async function p2(" : "async function e0(",
    modern ? "function m2(" : "function t0(",
    (modern ? MAIN_LOADER_26_908 : MAIN_LOADER) + mainHotReload(),
  );
  const transformed = Buffer.from(text, "utf8");
  new vm.Script(text, { filename: profile.main.archivePath });
  return transformed;
}


function transformRenderer(buffer, profile) {
  let text = buffer.toString("utf8");
  if (Buffer.byteLength(text, "utf8") !== buffer.length) throw new Error("Renderer bundle is not UTF-8");
  if (isCurrentPatch(buffer)) return buffer;
  text = stripLegacyUsage(text, "const __CODEX_SKIN_USAGE_RENDER_V1__=(()=>{");
  text = stripMarkerDeclaration(stripMarkerDeclaration(text, GIF_MARKER), HOT_RELOAD_MARKER);
  const queryClientGetter = resolveQueryClientGetter(text);
  const modern = MODERN_PROFILE_IDS.has(profile.id);
  text = replaceSection(
    text,
    modern ? "function Alo(e)" : "function eer(e)",
    modern ? "function jlo(" : "function ter(",
    (modern ? RENDERER_SOURCE_26_908 : RENDERER_SOURCE) + rendererHotReload(),
  );
  if (!text.includes("animationUrls:e.animationDataUrls")) {
    text = replaceOnce(
      text,
      "spritesheetUrl:e.spritesheetDataUrl,upgradeDirectoryPath",
      "spritesheetUrl:e.spritesheetDataUrl,animationUrls:e.animationDataUrls,upgradeDirectoryPath",
    );
  }
  text = replaceSection(
    text,
    modern ? "ruo=({assetMap:e" : "wer=({assetMap:e",
    modern ? "function auo(" : "function Eer(",
    withQueryClientCapture(modern ? RENDERER_COMPONENT_26_908 : RENDERER_COMPONENT, queryClientGetter),
  );
  const transformed = Buffer.from(text, "utf8");
  checkModuleSyntax(text, profile.renderer.archivePath);
  return transformed;
}


function isGifPatched(name, buffer) {
  const text = buffer.toString("utf8");
  if (name === "main") return text.includes("animationDataUrls:d") && text.includes("n.animationPaths??{}");
  return text.includes("animationUrls:e.animationUrls") && /backgroundRepeat:(?:f|h)!=null/.test(text);
}


function isCurrentPatch(buffer) {
  const text = buffer.toString("utf8");
  return text.includes(GIF_MARKER) && text.includes(HOT_RELOAD_MARKER);
}


function verifyIntegrity(entry, buffer) {
  if (entry.integrity == null) return false;
  const actual = integrityFor(buffer, Number(entry.integrity.blockSize));
  return entry.size === buffer.length
    && entry.integrity.algorithm === actual.algorithm
    && entry.integrity.hash === actual.hash
    && JSON.stringify(entry.integrity.blocks) === JSON.stringify(actual.blocks);
}


function serializeHeader(archive) {
  const json = Buffer.from(JSON.stringify(archive.header), "utf8");
  if (json.length > archive.headerPickle.length - 8) {
    throw new Error(`Updated ASAR header does not fit: ${json.length} > ${archive.headerPickle.length - 8}`);
  }
  const output = Buffer.alloc(archive.headerPickle.length);
  archive.headerPickle.copy(output, 0, 0, 4);
  output.writeUInt32LE(json.length, 4);
  json.copy(output, 8);
  return output;
}


function writeHeader(asarPath, headerPickle) {
  const fd = fs.openSync(asarPath, "r+");
  try {
    fs.writeSync(fd, headerPickle, 0, headerPickle.length, 8);
    fs.fsyncSync(fd);
  } finally {
    fs.closeSync(fd);
  }
}


function writeAtomic(filePath, buffer) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const temporary = `${filePath}.winefox-new`;
  fs.writeFileSync(temporary, buffer);
  fs.renameSync(temporary, filePath);
}


function parseArgs(argv) {
  const args = { action: null, asar: null, backupDir: null };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === "--action") args.action = argv[++index];
    else if (value === "--asar") args.asar = argv[++index];
    else if (value === "--backup-dir") args.backupDir = argv[++index];
    else throw new Error(`Unknown argument: ${value}`);
  }
  if (!args.action || !["plan", "apply", "restore", "verify"].includes(args.action)) {
    throw new Error("--action must be plan, apply, restore, or verify");
  }
  if (!args.asar) throw new Error("--asar is required");
  if (["apply", "restore"].includes(args.action) && !args.backupDir) {
    throw new Error("--backup-dir is required for apply and restore");
  }
  return args;
}


function inspect(asarPath) {
  const archive = readArchive(asarPath);
  const profile = TARGET_PROFILES.find((candidate) =>
    hasEntry(archive.header, candidate.main.archivePath)
      && hasEntry(archive.header, candidate.renderer.archivePath),
  );
  if (profile == null) throw new Error("Unsupported ASAR layout: pet runtime bundles were not found");
  const targets = {};
  for (const [name, config] of Object.entries({ main: profile.main, renderer: profile.renderer })) {
    const loaded = readEntry(asarPath, archive, config.archivePath);
    targets[name] = {
      archivePath: config.archivePath,
      buffer: loaded.buffer,
      entry: loaded.entry,
      externalPath: loaded.externalPath,
      sha256: sha256(loaded.buffer),
      integrityOk: verifyIntegrity(loaded.entry, loaded.buffer),
      gifPatched: isGifPatched(name, loaded.buffer),
      upToDate: isCurrentPatch(loaded.buffer),
    };
  }
  const unpackedCount = Object.values(targets).filter((target) => target.entry.unpacked === true).length;
  const state = unpackedCount === Object.keys(targets).length
    && Object.values(targets).every((target) => target.gifPatched && target.upToDate && target.integrityOk)
    ? "gif-patched"
    : unpackedCount === Object.keys(targets).length
      && Object.values(targets).every((target) => target.gifPatched && target.integrityOk)
      ? "gif-patched-legacy"
    : unpackedCount === 0
      ? "baseline"
      : "unknown";
  return { archive, profile, targets, state };
}


function report(asarPath, info, extra = {}) {
  return {
    ok: info.state !== "unknown" && Object.values(info.targets).every((target) => target.integrityOk),
    state: info.state,
    runtimeProfile: info.profile.id,
    asar: path.resolve(asarPath),
    asarSha256: sha256File(asarPath),
    headerSize: info.archive.headerSize,
    hotReload: hotReloadReport(info),
    targets: Object.fromEntries(Object.entries(info.targets).map(([name, target]) => [name, {
      archivePath: target.archivePath,
      externalPath: target.externalPath,
      bytes: target.buffer.length,
      sha256: target.sha256,
      unpacked: target.entry.unpacked === true,
      integrityOk: target.integrityOk,
      gifPatched: target.gifPatched,
      upToDate: target.upToDate,
    }])),
    ...extra,
  };
}


const args = parseArgs(process.argv.slice(2));
const asarPath = path.resolve(args.asar);
let info = inspect(asarPath);

if (args.action === "verify") {
  console.log(JSON.stringify(report(asarPath, info), null, 2));
  if (!report(asarPath, info).ok) process.exitCode = 1;
} else if (args.action === "plan") {
  const planningLegacyUpgrade = info.state === "gif-patched-legacy";
  if (info.state !== "baseline" && !planningLegacyUpgrade) throw new Error("Plan requires a supported baseline or legacy GIF patch");
  const transformed = {};
  for (const [name, target] of Object.entries(info.targets)) {
    const config = info.profile[name];
    const supported = planningLegacyUpgrade
      ? config.legacyPatchHashes.has(target.sha256)
      : config.supportedHashes.has(target.sha256);
    if (!supported || !target.integrityOk) {
      throw new Error(`Unsupported ${name} bundle: ${target.sha256}`);
    }
    transformed[name] = name === "main"
      ? transformMain(target.buffer, info.profile)
      : transformRenderer(target.buffer, info.profile);
    if (!isGifPatched(name, transformed[name]) || !isCurrentPatch(transformed[name])) {
      throw new Error(`${name} transform did not install all runtime markers`);
    }
  }
  console.log(JSON.stringify(report(asarPath, info, {
    upgradingLegacy: planningLegacyUpgrade,
    planned: Object.fromEntries(Object.entries(transformed).map(([name, buffer]) => [name, {
      bytes: buffer.length,
      sha256: sha256(buffer),
      gifPatched: isGifPatched(name, buffer),
      upToDate: isCurrentPatch(buffer),
    }])),
  }), null, 2));
} else if (args.action === "apply") {
  if (info.state === "gif-patched") {
    console.log(JSON.stringify(report(asarPath, info, { changed: false }), null, 2));
    process.exit(0);
  }
  const upgradingLegacy = info.state === "gif-patched-legacy";
  if (info.state !== "baseline" && !upgradingLegacy) throw new Error("Refusing to patch a mixed or unknown ASAR state");
  for (const [name, target] of Object.entries(info.targets)) {
    const config = info.profile[name];
    const supported = upgradingLegacy
      ? config.legacyPatchHashes.has(target.sha256)
      : config.supportedHashes.has(target.sha256);
    if (!supported || !target.integrityOk) {
      throw new Error(`Unsupported ${name} bundle: ${target.sha256}`);
    }
  }

  const backupDir = path.resolve(args.backupDir);
  fs.mkdirSync(backupDir, { recursive: true });
  const headerBackup = path.join(backupDir, "header-baseline.pickle");
  if (fs.existsSync(headerBackup)) {
    if (!upgradingLegacy && !fs.readFileSync(headerBackup).equals(info.archive.headerPickle)) {
      throw new Error(`Existing header backup belongs to a different ASAR state: ${headerBackup}`);
    }
    if (upgradingLegacy && fs.readFileSync(headerBackup).length !== info.archive.headerPickle.length) {
      throw new Error(`Existing header backup belongs to a different ASAR: ${headerBackup}`);
    }
  } else {
    if (upgradingLegacy) throw new Error("Legacy patch upgrade requires the original header backup");
    fs.writeFileSync(headerBackup, info.archive.headerPickle);
  }
  if (!upgradingLegacy) fs.writeFileSync(path.join(backupDir, "baseline-report.json"), JSON.stringify(report(asarPath, info), null, 2));

  const transformed = {
    main: transformMain(info.targets.main.buffer, info.profile),
    renderer: transformRenderer(info.targets.renderer.buffer, info.profile),
  };
  const previousHeader = Buffer.from(info.archive.headerPickle);
  const previousTargets = Object.fromEntries(Object.entries(info.targets).map(([name, target]) => [name, Buffer.from(target.buffer)]));
  try {
    for (const [name, buffer] of Object.entries(transformed)) {
      if (!isGifPatched(name, buffer) || !isCurrentPatch(buffer)) throw new Error(`${name} transform did not install all runtime markers`);
      const target = info.targets[name];
      const externalPath = overlayPath(asarPath, target.archivePath);
      writeAtomic(externalPath, buffer);
      target.entry.size = buffer.length;
      delete target.entry.offset;
      target.entry.unpacked = true;
      target.entry.integrity = integrityFor(buffer, Number(target.entry.integrity?.blockSize ?? 4194304));
    }
    const updatedHeader = serializeHeader(info.archive);
    writeHeader(asarPath, updatedHeader);
    info = inspect(asarPath);
    if (info.state !== "gif-patched") throw new Error("Post-patch verification failed");
  } catch (error) {
    for (const [name, target] of Object.entries(info.targets)) {
      const archivePath = info.profile[name].archivePath;
      const externalPath = overlayPath(asarPath, archivePath);
      if (upgradingLegacy) writeAtomic(externalPath, previousTargets[name]);
      else fs.rmSync(externalPath, { force: true });
    }
    writeHeader(asarPath, previousHeader);
    throw error;
  }
  const appliedReport = report(asarPath, info, { changed: true, upgradedLegacy: upgradingLegacy });
  fs.writeFileSync(path.join(backupDir, "gif-patched-report.json"), JSON.stringify(appliedReport, null, 2));
  console.log(JSON.stringify(appliedReport, null, 2));
} else if (args.action === "restore") {
  if (info.state === "baseline") {
    console.log(JSON.stringify(report(asarPath, info, { changed: false }), null, 2));
    process.exit(0);
  }
  if (info.state !== "gif-patched" && info.state !== "gif-patched-legacy") throw new Error("Refusing to restore a mixed or unknown ASAR state");
  const headerBackup = path.join(path.resolve(args.backupDir), "header-baseline.pickle");
  const baselineHeader = fs.readFileSync(headerBackup);
  if (baselineHeader.length !== info.archive.headerPickle.length) {
    throw new Error("Baseline header size does not match this ASAR");
  }
  writeHeader(asarPath, baselineHeader);
  for (const target of Object.values(info.targets)) {
    fs.rmSync(overlayPath(asarPath, target.archivePath), { force: true });
  }
  info = inspect(asarPath);
  if (info.state !== "baseline") throw new Error("Restore verification failed");
  console.log(JSON.stringify(report(asarPath, info, { changed: true }), null, 2));
}

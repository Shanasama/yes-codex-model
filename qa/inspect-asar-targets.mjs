import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const asarPath = path.resolve(process.argv[2] || "");
if (!asarPath || !fs.existsSync(asarPath)) {
  throw new Error("Usage: node qa/inspect-asar-targets.mjs <app.asar>");
}

function readArchive(filePath) {
  const fd = fs.openSync(filePath, "r");
  try {
    const sizePickle = Buffer.alloc(8);
    fs.readSync(fd, sizePickle, 0, sizePickle.length, 0);
    const headerSize = sizePickle.readUInt32LE(4);
    const headerPickle = Buffer.alloc(headerSize);
    fs.readSync(fd, headerPickle, 0, headerPickle.length, 8);
    const jsonSize = headerPickle.readUInt32LE(4);
    return {
      header: JSON.parse(headerPickle.subarray(8, 8 + jsonSize).toString("utf8")),
      baseOffset: 8 + headerSize,
    };
  } finally {
    fs.closeSync(fd);
  }
}

function entries(node, prefix = "") {
  const result = [];
  for (const [name, entry] of Object.entries(node.files || {})) {
    const archivePath = prefix ? `${prefix}/${name}` : name;
    if (entry.files) result.push(...entries(entry, archivePath));
    else result.push({ archivePath, entry });
  }
  return result;
}

function readEntry(archive, archivePath, entry) {
  if (entry.unpacked === true) {
    return fs.readFileSync(path.join(`${asarPath}.unpacked`, ...archivePath.split("/")));
  }
  const size = Number(entry.size);
  const buffer = Buffer.alloc(size);
  const fd = fs.openSync(asarPath, "r");
  try {
    const count = fs.readSync(fd, buffer, 0, size, archive.baseOffset + Number(entry.offset));
    if (count !== size) throw new Error(`Short read: ${archivePath}`);
    return buffer;
  } finally {
    fs.closeSync(fd);
  }
}

function occurrences(text, needle) {
  let count = 0;
  let offset = 0;
  while ((offset = text.indexOf(needle, offset)) >= 0) {
    count += 1;
    offset += needle.length;
  }
  return count;
}

const anchors = [
  "spritesheetPath",
  "spriteVersionNumber",
  "readFileBase64",
  "spritesheetDataUrl",
  "data-codex-pet-state",
  "lookFrame",
  "respondToHover",
  "assetMap",
  "animationPaths",
  "animationUrls",
];
const archive = readArchive(asarPath);
const candidates = entries(archive.header).filter(({ archivePath }) =>
  /^(\.vite\/build\/(?:src|main|core)-.*\.js|webview\/assets\/app-initial-.*\.js)$/.test(archivePath),
);

const report = candidates.map(({ archivePath, entry }) => {
  const buffer = readEntry(archive, archivePath, entry);
  const text = buffer.toString("utf8");
  const counts = Object.fromEntries(anchors.map((anchor) => [anchor, occurrences(text, anchor)]));
  const marker = anchors.find((anchor) => counts[anchor] > 0);
  const markerIndex = marker ? text.indexOf(marker) : -1;
  return {
    archivePath,
    bytes: buffer.length,
    sha256: crypto.createHash("sha256").update(buffer).digest("hex"),
    unpacked: entry.unpacked === true,
    counts,
    marker,
    snippet: markerIndex >= 0 ? text.slice(Math.max(0, markerIndex - 500), markerIndex + 1200) : null,
  };
});

console.log(JSON.stringify({ asar: asarPath, candidates: report }, null, 2));

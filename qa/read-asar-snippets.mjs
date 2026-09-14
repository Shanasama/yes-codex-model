import fs from "node:fs";
import path from "node:path";

const [asarArg, archivePath, ...markers] = process.argv.slice(2);
const asarPath = path.resolve(asarArg || "");
if (!asarArg || !archivePath || !markers.length || !fs.existsSync(asarPath)) {
  throw new Error("Usage: node qa/read-asar-snippets.mjs <app.asar> <entry> <marker> [...marker]");
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

function findEntry(header) {
  let entry = header;
  for (const part of archivePath.split("/")) entry = entry.files?.[part];
  if (!entry) throw new Error(`ASAR entry not found: ${archivePath}`);
  return entry;
}

function readEntry(archive, entry) {
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

const archive = readArchive(asarPath);
const text = readEntry(archive, findEntry(archive.header)).toString("utf8");
const snippets = markers.map((marker) => {
  const index = text.indexOf(marker);
  return {
    marker,
    index,
    occurrenceCount: index < 0 ? 0 : text.split(marker).length - 1,
    snippet: index < 0 ? null : text.slice(Math.max(0, index - 2200), index + marker.length + 4200),
  };
});
console.log(JSON.stringify({ asar: asarPath, archivePath, snippets }, null, 2));

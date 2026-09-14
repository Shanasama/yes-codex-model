import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";

const MAX_ENTRIES = 80;
const MAX_ENTRY_BYTES = 128 * 1024 * 1024;
const MAX_TOTAL_BYTES = 512 * 1024 * 1024;

const CRC_TABLE = new Uint32Array(256);
for (let n = 0; n < 256; n += 1) {
  let c = n;
  for (let k = 0; k < 8; k += 1) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
  CRC_TABLE[n] = c >>> 0;
}

export function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) crc = CRC_TABLE[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function safeArchiveName(name) {
  if (!name || name.includes("\\") || name.includes("\0") || name.startsWith("/") || /^[A-Za-z]:/.test(name)) {
    throw new Error(`压缩包包含危险路径：${name}`);
  }
  const parts = name.split("/").filter(Boolean);
  if (!parts.length || parts.some((part) => part === "." || part === "..")) throw new Error(`压缩包包含危险路径：${name}`);
  return parts.join("/");
}

function dosTimestamp(date = new Date()) {
  const year = Math.max(1980, date.getFullYear());
  const time = (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2);
  const day = ((year - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate();
  return { time, day };
}

export function createZip(entries, outputPath) {
  if (!Array.isArray(entries) || !entries.length || entries.length > MAX_ENTRIES) throw new Error("压缩包文件数量无效");
  const localParts = [];
  const centralParts = [];
  let offset = 0;
  let totalBytes = 0;
  const { time, day } = dosTimestamp();

  for (const entry of entries) {
    const name = safeArchiveName(entry.name);
    const nameBuffer = Buffer.from(name, "utf8");
    const data = Buffer.isBuffer(entry.data) ? entry.data : fs.readFileSync(entry.path);
    if (data.length > MAX_ENTRY_BYTES) throw new Error(`文件过大：${name}`);
    totalBytes += data.length;
    if (totalBytes > MAX_TOTAL_BYTES) throw new Error("皮肤包总大小超过 512MB");
    const checksum = crc32(data);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(0, 8);
    local.writeUInt16LE(time, 10);
    local.writeUInt16LE(day, 12);
    local.writeUInt32LE(checksum, 14);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBuffer.length, 26);
    localParts.push(local, nameBuffer, data);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(0x0314, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(0, 10);
    central.writeUInt16LE(time, 12);
    central.writeUInt16LE(day, 14);
    central.writeUInt32LE(checksum, 16);
    central.writeUInt32LE(data.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBuffer.length, 28);
    central.writeUInt32LE(0, 38);
    central.writeUInt32LE(offset, 42);
    centralParts.push(central, nameBuffer);
    offset += local.length + nameBuffer.length + data.length;
  }

  const centralDirectory = Buffer.concat(centralParts);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(centralDirectory.length, 12);
  end.writeUInt32LE(offset, 16);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  const temporary = `${outputPath}.new`;
  fs.writeFileSync(temporary, Buffer.concat([...localParts, centralDirectory, end]));
  fs.renameSync(temporary, outputPath);
  return outputPath;
}

function findEndRecord(buffer) {
  const lower = Math.max(0, buffer.length - 65557);
  for (let offset = buffer.length - 22; offset >= lower; offset -= 1) {
    if (buffer.readUInt32LE(offset) === 0x06054b50) return offset;
  }
  throw new Error("不是有效的 .codexskin ZIP 文件");
}

export function readZip(input) {
  const buffer = Buffer.isBuffer(input) ? input : fs.readFileSync(input);
  const endOffset = findEndRecord(buffer);
  const count = buffer.readUInt16LE(endOffset + 10);
  const centralSize = buffer.readUInt32LE(endOffset + 12);
  let cursor = buffer.readUInt32LE(endOffset + 16);
  if (!count || count > MAX_ENTRIES || cursor + centralSize > buffer.length) throw new Error("皮肤包目录无效");

  const entries = [];
  let totalBytes = 0;
  for (let index = 0; index < count; index += 1) {
    if (cursor + 46 > buffer.length || buffer.readUInt32LE(cursor) !== 0x02014b50) throw new Error("皮肤包目录损坏");
    const flags = buffer.readUInt16LE(cursor + 8);
    const method = buffer.readUInt16LE(cursor + 10);
    const checksum = buffer.readUInt32LE(cursor + 16);
    const compressedSize = buffer.readUInt32LE(cursor + 20);
    const size = buffer.readUInt32LE(cursor + 24);
    const nameLength = buffer.readUInt16LE(cursor + 28);
    const extraLength = buffer.readUInt16LE(cursor + 30);
    const commentLength = buffer.readUInt16LE(cursor + 32);
    const externalAttrs = buffer.readUInt32LE(cursor + 38);
    const localOffset = buffer.readUInt32LE(cursor + 42);
    const name = safeArchiveName(buffer.subarray(cursor + 46, cursor + 46 + nameLength).toString("utf8"));
    if (flags & 1) throw new Error(`不支持加密文件：${name}`);
    if (method !== 0 && method !== 8) throw new Error(`不支持的压缩方式：${name}`);
    if (((externalAttrs >>> 16) & 0xf000) === 0xa000) throw new Error(`皮肤包不得包含符号链接：${name}`);
    if (size > MAX_ENTRY_BYTES) throw new Error(`解压文件过大：${name}`);
    totalBytes += size;
    if (totalBytes > MAX_TOTAL_BYTES) throw new Error("皮肤包解压后超过 512MB");
    if (localOffset + 30 > buffer.length || buffer.readUInt32LE(localOffset) !== 0x04034b50) throw new Error(`文件头损坏：${name}`);
    const localNameLength = buffer.readUInt16LE(localOffset + 26);
    const localExtraLength = buffer.readUInt16LE(localOffset + 28);
    const dataOffset = localOffset + 30 + localNameLength + localExtraLength;
    if (dataOffset + compressedSize > buffer.length) throw new Error(`文件数据被截断：${name}`);
    const compressed = buffer.subarray(dataOffset, dataOffset + compressedSize);
    const data = method === 0 ? Buffer.from(compressed) : zlib.inflateRawSync(compressed, { maxOutputLength: MAX_ENTRY_BYTES });
    if (data.length !== size || crc32(data) !== checksum) throw new Error(`文件校验失败：${name}`);
    entries.push({ name, data });
    cursor += 46 + nameLength + extraLength + commentLength;
  }
  return entries;
}

export const ZIP_LIMITS = Object.freeze({ maxEntries: MAX_ENTRIES, maxEntryBytes: MAX_ENTRY_BYTES, maxTotalBytes: MAX_TOTAL_BYTES });

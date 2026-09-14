const MAX_GIF_BYTES = 128 * 1024 * 1024;
const MAX_DIMENSION = 4096;
const MAX_FRAMES = 4096;

function readSubBlocks(buffer, offset) {
  let cursor = offset;
  let bytes = 0;
  while (cursor < buffer.length) {
    const size = buffer[cursor++];
    if (size === 0) return { offset: cursor, bytes };
    if (cursor + size > buffer.length) throw new Error("GIF 数据块被截断");
    bytes += size;
    cursor += size;
  }
  throw new Error("GIF 数据块缺少结束标记");
}

export function inspectGif(buffer) {
  if (!Buffer.isBuffer(buffer)) buffer = Buffer.from(buffer);
  if (buffer.length < 14 || buffer.length > MAX_GIF_BYTES) {
    throw new Error(`GIF 大小必须在 14 字节到 ${MAX_GIF_BYTES / 1024 / 1024}MB 之间`);
  }
  const signature = buffer.subarray(0, 6).toString("ascii");
  if (signature !== "GIF87a" && signature !== "GIF89a") throw new Error("文件不是有效 GIF");

  const width = buffer.readUInt16LE(6);
  const height = buffer.readUInt16LE(8);
  if (!width || !height || width > MAX_DIMENSION || height > MAX_DIMENSION) {
    throw new Error(`GIF 尺寸超出限制：${width}x${height}`);
  }

  const packed = buffer[10];
  let offset = 13;
  if (packed & 0x80) offset += 3 * (2 ** ((packed & 0x07) + 1));

  let frames = 0;
  let durationMs = 0;
  let pendingDelayMs = 0;
  let loopCount = null;

  while (offset < buffer.length) {
    const marker = buffer[offset++];
    if (marker === 0x3b) break;
    if (marker === 0x2c) {
      if (offset + 9 > buffer.length) throw new Error("GIF 图像描述符被截断");
      const imagePacked = buffer[offset + 8];
      offset += 9;
      if (imagePacked & 0x80) offset += 3 * (2 ** ((imagePacked & 0x07) + 1));
      if (offset >= buffer.length) throw new Error("GIF 缺少 LZW 数据");
      offset += 1;
      const imageData = readSubBlocks(buffer, offset);
      offset = imageData.offset;
      frames += 1;
      if (frames > MAX_FRAMES) throw new Error(`GIF 帧数超过 ${MAX_FRAMES}`);
      durationMs += pendingDelayMs || 100;
      pendingDelayMs = 0;
      continue;
    }
    if (marker === 0x21) {
      if (offset >= buffer.length) throw new Error("GIF 扩展块被截断");
      const label = buffer[offset++];
      if (label === 0xf9) {
        const size = buffer[offset++];
        if (size !== 4 || offset + 5 > buffer.length) throw new Error("GIF 帧控制块无效");
        pendingDelayMs = buffer.readUInt16LE(offset + 1) * 10;
        offset += 4;
        if (buffer[offset++] !== 0) throw new Error("GIF 帧控制块缺少结束标记");
      } else if (label === 0xff) {
        const blockSize = buffer[offset++];
        if (offset + blockSize > buffer.length) throw new Error("GIF 应用扩展块被截断");
        const appId = buffer.subarray(offset, offset + blockSize).toString("ascii");
        offset += blockSize;
        const start = offset;
        const blocks = readSubBlocks(buffer, offset);
        if (appId.startsWith("NETSCAPE") && start + 4 <= buffer.length && buffer[start] === 3 && buffer[start + 1] === 1) {
          loopCount = buffer.readUInt16LE(start + 2);
        }
        offset = blocks.offset;
      } else {
        const blocks = readSubBlocks(buffer, offset);
        offset = blocks.offset;
      }
      continue;
    }
    throw new Error(`GIF 中出现未知块 0x${marker.toString(16)}`);
  }

  if (frames === 0) throw new Error("GIF 不包含动画帧");
  return {
    format: signature,
    width,
    height,
    frames,
    durationMs,
    averageFrameMs: Math.round(durationMs / frames),
    loopCount,
    bytes: buffer.length
  };
}

export const GIF_LIMITS = Object.freeze({ maxBytes: MAX_GIF_BYTES, maxDimension: MAX_DIMENSION, maxFrames: MAX_FRAMES });

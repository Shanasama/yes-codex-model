using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SkinStudio.Core;

/// <summary>
/// 按需解码的 GIF 播放器：只保留最近若干帧的缩放位图，避免大 GIF 一次性解码占满内存。
/// </summary>
internal sealed class GifAnimation : IDisposable
{
    private const int DefaultDelayMs = 100;
    private const int MinCacheFrames = 4;
    private const int MaxCacheFrames = 28;
    private const long FrameCacheBudget = 4L * 1024 * 1024;

    private readonly Image _image;
    private readonly MemoryStream _stream;
    private readonly FrameDimension _dimension;
    private readonly int[] _delays;
    private readonly Dictionary<int, LinkedListNode<CacheItem>> _index = new();
    private readonly LinkedList<CacheItem> _lru = new();

    private GifAnimation(MemoryStream stream, Image image, int[] delays)
    {
        _stream = stream;
        _image = image;
        _delays = delays;
        _dimension = new FrameDimension(image.FrameDimensionsList[0]);
        FrameCount = Math.Max(1, image.GetFrameCount(_dimension));
        SourceSize = new Size(image.Width, image.Height);
        _image.SelectActiveFrame(_dimension, 0);
    }

    public int FrameCount { get; }
    public Size SourceSize { get; }

    public static GifAnimation Load(byte[] bytes)
    {
        var stream = new MemoryStream(bytes, writable: false);
        try
        {
            var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            return new GifAnimation(stream, image, ReadDelays(image));
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static int[] ReadDelays(Image image)
    {
        try
        {
            var item = image.GetPropertyItem(0x5100);
            if (item?.Value is { Length: >= 4 } value)
            {
                var count = value.Length / 4;
                var delays = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var delay = BitConverter.ToInt32(value, i * 4);
                    delays[i] = delay <= 0 ? DefaultDelayMs : Math.Clamp(delay * 10, 20, 1000);
                }
                return delays;
            }
        }
        catch
        {
            // 没有帧延迟信息时退回默认值
        }
        return Array.Empty<int>();
    }

    public int DelayFor(int frame)
    {
        if (_delays.Length == 0) return DefaultDelayMs;
        return _delays[((frame % _delays.Length) + _delays.Length) % _delays.Length];
    }

    /// <summary>取某帧的缩放位图；target 为期望的显示尺寸。</summary>
    public Bitmap? Frame(int frameIndex, Size target, bool cache = true)
    {
        if (target.Width <= 0 || target.Height <= 0) return null;
        frameIndex = ((frameIndex % FrameCount) + FrameCount) % FrameCount;

        if (cache && _index.TryGetValue(frameIndex, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return node.Value.Bitmap;
        }

        var bitmap = Decode(frameIndex, target);
        if (bitmap is null) return null;

        if (cache)
        {
            var newNode = _lru.AddFirst(new CacheItem(frameIndex, bitmap, target));
            _index[frameIndex] = newNode;
            var frameBytes = Math.Max(1L, (long)target.Width * target.Height * 4);
            var limit = (int)Math.Clamp(FrameCacheBudget / frameBytes, MinCacheFrames, MaxCacheFrames);
            while (_lru.Count > limit)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _index.Remove(last.Value.Frame);
                last.Value.Bitmap.Dispose();
            }
        }
        return bitmap;
    }

    private Bitmap? Decode(int frameIndex, Size target)
    {
        try
        {
            _image.SelectActiveFrame(_dimension, frameIndex);
            var bitmap = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.InterpolationMode = target.Width >= SourceSize.Width * 2
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(_image, new Rectangle(0, 0, target.Width, target.Height),
                0, 0, SourceSize.Width, SourceSize.Height, GraphicsUnit.Pixel);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>清空缓存（舞台尺寸变化或切换状态时调用）。</summary>
    public void ResetCache()
    {
        foreach (var item in _lru) item.Bitmap.Dispose();
        _lru.Clear();
        _index.Clear();
    }

    public void Dispose()
    {
        ResetCache();
        _image.Dispose();
        _stream.Dispose();
    }

    private sealed record CacheItem(int Frame, Bitmap Bitmap, Size Target);
}

internal static class GifThumbnails
{
    /// <summary>只解码首帧并等比缩放到指定尺寸，用于列表缩略图。</summary>
    public static Bitmap? FirstFrame(byte[] bytes, Size target)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var image = Image.FromStream(stream, false, false);
            var dimension = new FrameDimension(image.FrameDimensionsList[0]);
            image.SelectActiveFrame(dimension, 0);

            var ratio = Math.Min((double)target.Width / image.Width, (double)target.Height / image.Height);
            var width = Math.Max(1, (int)Math.Round(image.Width * ratio));
            var height = Math.Max(1, (int)Math.Round(image.Height * ratio));
            var bitmap = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.InterpolationMode = ratio >= 2 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, new Rectangle((target.Width - width) / 2, (target.Height - height) / 2, width, height));
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

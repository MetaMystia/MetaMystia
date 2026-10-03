using System;
using System.Buffers.Binary;
using System.IO;

using Mystia.Assets;
using Mystia.Imgui;
using Mystia.Numerics;

namespace MetaMystia;

/// <summary>
/// PNG 文件头：只解析 IHDR 的像素尺寸，不解码。
/// 资产 API 构建的贴图是不透明句柄，没有尺寸查询，而「把整张贴图切成精灵」与
/// 「按包内声明的矩形校验切图」都需要尺寸，因此这里读取文件头（框架的解码器读到同一份数据）。
/// </summary>
internal static class PngHeader
{
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryReadSize(ReadOnlySpan<byte> png, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (png.Length < 24 || !png.Slice(0, 8).SequenceEqual(Signature) || !png.Slice(12, 4).SequenceEqual("IHDR"u8))
            return false;

        width = BinaryPrimitives.ReadInt32BigEndian(png.Slice(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(png.Slice(20, 4));
        return width > 0 && height > 0;
    }
}

[AutoLog]
public static partial class Utils
{
    private const float DefaultPixelsPerUnit = 48f;

    /// <summary>读一张 PNG 并切成精灵；像素单位、pivot 语义与迁移前一致。</summary>
    public static SpriteHandle GetArtWork(string filePath, Vector2 pivot, int width = 0, int height = 0, int pixelOffsetX = 0, int pixelOffsetY = 0)
    {
        if (!File.Exists(filePath)) return null;
        return GetArtWorkFromBytes(File.ReadAllBytes(filePath), pivot, width, height, pixelOffsetX, pixelOffsetY);
    }

    /// <summary>
    /// 把 PNG 字节切成精灵。给定 <paramref name="width"/>/<paramref name="height"/> 时按原居中偏移取有效矩形；
    /// 资产 API 不能新建贴图再逐像素拷贝，因此不再补透明边框，超出源贴图的部分直接丢弃。
    /// </summary>
    public static SpriteHandle GetArtWorkFromBytes(byte[] fileData, Vector2 pivot, int width = 0, int height = 0, int pixelOffsetX = 0, int pixelOffsetY = 0)
    {
        if (ModRuntime.Assets is not { } assets)
        {
            Log.LogWarning("Asset factory unavailable; the artwork was not built.");
            return null;
        }

        if (!PngHeader.TryReadSize(fileData, out var sourceWidth, out var sourceHeight)
            || !assets.TryCreateTexture(fileData, out var texture))
        {
            Log.LogWarning("Failed to decode the artwork (PNG only).");
            return null;
        }

        var rect = new Rect(0f, 0f, sourceWidth, sourceHeight);
        if (width > 0 && height > 0 && (sourceWidth != width || sourceHeight != height))
        {
            // 迁移前把源贴图按 (width, height) 居中（偏移 pixelOffset）画进新贴图；这里取两者相交的部分。
            var offsetX = (width - sourceWidth) / 2 + pixelOffsetX;
            var offsetY = (height - sourceHeight) / 2 + pixelOffsetY;
            var x0 = Math.Max(0, -offsetX);
            var y0 = Math.Max(0, -offsetY);
            var x1 = Math.Min(sourceWidth, width - offsetX);
            var y1 = Math.Min(sourceHeight, height - offsetY);
            if (x1 <= x0 || y1 <= y0)
            {
                Log.LogWarning($"The artwork ({sourceWidth}×{sourceHeight}) does not overlap the requested {width}×{height}.");
                return null;
            }

            rect = new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        if (!assets.TryCreateSprite(texture, rect, pivot, DefaultPixelsPerUnit, out var sprite))
        {
            Log.LogWarning($"Failed to cut the artwork sprite {rect.X},{rect.Y} {rect.Width}×{rect.Height}.");
            return null;
        }

        return sprite;
    }

    /// <summary>全透明像素集：框架建一张空像素贴图，再切成整图精灵。</summary>
    public static SpriteHandle BuildEmptySprite(int width = 64, int height = 64)
    {
        if (ModRuntime.Assets is not { } assets)
        {
            Log.LogWarning("Asset factory unavailable; the empty sprite was not built.");
            return null;
        }

        if (!assets.TryCreatePixelTexture(width, height, out var pixels))
        {
            Log.LogWarning($"Failed to create a {width}×{height} pixel texture.");
            return null;
        }

        // PixelBuffer 初始即全透明，无需填充即可切图。
        return assets.TryCreateSprite(
            pixels.Texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            DefaultPixelsPerUnit,
            out var sprite)
            ? sprite
            : null;
    }
}

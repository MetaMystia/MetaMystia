using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

using Mystia.Assets;
using Mystia.Imgui;
using Mystia.Numerics;

namespace MetaMystia.ResourceEx.AssetManagement;

public readonly struct RexUri
{
    public const string Scheme = "rex";
    public const string Prefix = Scheme + "://";

    public string PackageName { get; }
    public string Path { get; }
    public string Value { get; }

    private RexUri(string packageName, string path)
    {
        PackageName = packageName;
        Path = path;
        Value = $"{Prefix}{packageName}/{path}";
    }

    public static bool IsRexUri(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParse(string value, out RexUri uri)
    {
        uri = default;

        if (!IsRexUri(value))
            return false;

        var remainder = value.Substring(Prefix.Length).Replace('\\', '/');
        var separator = remainder.IndexOf('/');
        if (separator <= 0 || separator >= remainder.Length - 1)
            return false;

        var packageName = remainder.Substring(0, separator).Trim();
        var path = NormalizePath(remainder.Substring(separator + 1));

        if (!IsValidPackageName(packageName) || string.IsNullOrEmpty(path))
            return false;

        uri = new RexUri(packageName, path);
        return true;
    }

    public static bool TryBuild(string packageName, string path, out RexUri uri)
    {
        uri = default;

        var normalizedPath = NormalizePath(path);
        if (!IsValidPackageName(packageName) || string.IsNullOrEmpty(normalizedPath))
            return false;

        uri = new RexUri(packageName.Trim(), normalizedPath);
        return true;
    }

    public static bool IsValidPackageName(string packageName)
    {
        return !string.IsNullOrWhiteSpace(packageName)
            && packageName.IndexOf('/') < 0
            && packageName.IndexOf('\\') < 0;
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Trim().Replace('\\', '/');

        if (System.IO.Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
            return null;

        var parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "." || parts[i] == "..")
                return null;
        }

        return string.Join("/", parts);
    }

    public override string ToString() => Value;
}

public enum RexAssetKind
{
    Image,
    Text,
    Audio,
    Binary
}

public abstract class RexAsset
{
    protected RexAsset(string uri, string packageName, string path, byte[] bytes, RexAssetKind kind)
    {
        Uri = uri;
        PackageName = packageName;
        Path = path;
        Bytes = bytes;
        Kind = kind;
    }

    public string Uri { get; }
    public string PackageName { get; }
    public string Path { get; }
    public byte[] Bytes { get; }
    public RexAssetKind Kind { get; }
}

/// <summary>
/// 包内图片：贴图与精灵都由框架的 <see cref="IAssetFactory"/> 构建，这里只保管句柄，因此不再有 Unity 对象。
/// 尺寸取自 PNG 文件头（资产 API 的句柄没有尺寸查询），用于按包内声明校验切图矩形与切整图精灵。
/// 精灵切图失败（尺寸与句柄不一致等）时 <see cref="Sprite"/> 为空，贴图仍然可用。
/// </summary>
public sealed class RexImageAsset : RexAsset
{
    public RexImageAsset(
        string uri, string packageName, string path, byte[] bytes,
        TextureHandle texture, SpriteHandle? sprite, int width, int height)
        : base(uri, packageName, path, bytes, RexAssetKind.Image)
    {
        Texture = texture;
        Sprite = sprite;
        Width = width;
        Height = height;
    }

    /// <summary>整张贴图切出的精灵。</summary>
    public TextureHandle Texture { get; }

    /// <summary>整张贴图的精灵。</summary>
    public SpriteHandle? Sprite { get; }

    public int Width { get; }
    public int Height { get; }
}

public sealed class RexTextAsset : RexAsset
{
    public RexTextAsset(string uri, string packageName, string path, byte[] bytes, string text)
        : base(uri, packageName, path, bytes, RexAssetKind.Text)
    {
        Text = text;
    }

    public string Text { get; }
}

/// <summary>包内音频：解码后的采样交给框架建剪辑，这里只保管句柄；无法解码的容器（OGG/MP3 等）<see cref="Clip"/> 为空。</summary>
public sealed class RexAudioAsset : RexAsset
{
    public RexAudioAsset(string uri, string packageName, string path, byte[] bytes, AudioClipHandle? clip = null)
        : base(uri, packageName, path, bytes, RexAssetKind.Audio)
    {
        Clip = clip;
    }

    public AudioClipHandle? Clip { get; }
}

public sealed class RexBinaryAsset : RexAsset
{
    public RexBinaryAsset(string uri, string packageName, string path, byte[] bytes)
        : base(uri, packageName, path, bytes, RexAssetKind.Binary)
    {
    }
}

[AutoLog]
public static partial class RexAssetRegistry
{
    // Case-sensitive: rex:// URIs follow RFC 3986 path semantics. The scheme prefix itself
    // is matched case-insensitively in RexUri.IsRexUri (per RFC 3986), but package name and
    // path are exact-match. This stays in lockstep with the framework's IAssetLocator, which
    // files a key under the MD5 of that exact key — so a single source of truth across both.
    private static readonly Dictionary<string, RexAsset> _assets = new(StringComparer.Ordinal);

    /// <summary>整张贴图切精灵时的 pixels per unit，与迁移前一致。</summary>
    private const float DefaultPixelsPerUnit = 48f;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".txt", ".md", ".csv", ".tsv", ".xml", ".yaml", ".yml"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac"
    };

    public static IReadOnlyDictionary<string, RexAsset> Assets => _assets;

    public static void RegisterPackage(LoadedResourcePackage package)
    {
        if (package?.AssetPackage == null || string.IsNullOrWhiteSpace(package.PackageLabel))
            return;

        int imageCount = 0;
        int textCount = 0;
        int audioCount = 0;
        int binaryCount = 0;

        foreach (var relativePath in package.AssetPackage.GetFilePaths())
        {
            if (!RexUri.TryBuild(package.PackageLabel, relativePath, out var rexUri))
            {
                Log.LogWarning($"[{package.PackageName}] Skipping invalid resource path: {relativePath}");
                continue;
            }

            var bytes = package.AssetPackage.GetBytes(relativePath);
            if (bytes == null)
            {
                Log.LogWarning($"[{package.PackageName}] Failed to read resource bytes: {relativePath}");
                continue;
            }

            var asset = CreateAsset(rexUri, bytes);
            RegisterAsset(asset);

            switch (asset.Kind)
            {
                case RexAssetKind.Image:
                    imageCount++;
                    break;
                case RexAssetKind.Text:
                    textCount++;
                    break;
                case RexAssetKind.Audio:
                    audioCount++;
                    break;
                default:
                    binaryCount++;
                    break;
            }
        }

        Log.LogInfo(
            $"[{package.PackageName}] Registered rex assets for {package.PackageLabel}: " +
            $"{imageCount} image(s), {textCount} text file(s), {audioCount} audio file(s), {binaryCount} binary file(s).");
    }

    public static bool TryResolveUri(string assetPath, string packageName, out string uri)
    {
        uri = null;

        if (RexUri.TryParse(assetPath, out var parsed))
        {
            uri = parsed.Value;
            return true;
        }

        if (!RexUri.TryBuild(packageName, assetPath, out parsed))
            return false;

        uri = parsed.Value;
        return true;
    }

    /// <summary>取某个 rex URI 登记进游戏资产管线的精灵。</summary>
    public static bool TryGetSprite(string uri, [NotNullWhen(true)] out SpriteHandle sprite)
    {
        sprite = null;
        return RexUri.IsRexUri(uri)
            && ModRuntime.Locator is { } locator
            && locator.TryResolveSprite(uri, out sprite);
    }

    /// <summary>取某个 rex URI 的资产引用（该 URI 登记为图片时才有）。</summary>
    public static bool TryGetSpriteReference(string uri, [NotNullWhen(true)] out AssetReference reference)
    {
        reference = null;
        return _assets.TryGetValue(uri, out var asset)
            && asset.Kind == RexAssetKind.Image
            && ModRuntime.Locator is { } locator
            && locator.TryGetReference(uri, out reference);
    }

    /// <summary>取某个 rex URI 的资产引用（该 URI 登记为音频时才有）。</summary>
    public static bool TryGetAudioReference(string uri, [NotNullWhen(true)] out AssetReference reference)
    {
        reference = null;
        return _assets.TryGetValue(uri, out var asset)
            && asset.Kind == RexAssetKind.Audio
            && ModRuntime.Locator is { } locator
            && locator.TryGetReference(uri, out reference);
    }

    private static RexAsset CreateAsset(RexUri uri, byte[] bytes)
    {
        var extension = System.IO.Path.GetExtension(uri.Path);

        if (ImageExtensions.Contains(extension))
            return CreateImageAsset(uri, bytes);

        if (TextExtensions.Contains(extension))
            return new RexTextAsset(uri.Value, uri.PackageName, uri.Path, bytes, Encoding.UTF8.GetString(bytes));

        if (AudioExtensions.Contains(extension))
            return CreateAudioAsset(uri, bytes);

        return new RexBinaryAsset(uri.Value, uri.PackageName, uri.Path, bytes);
    }

    private static RexAsset CreateImageAsset(RexUri uri, byte[] bytes)
    {
        // 尺寸来自 PNG 文件头：容器不是 PNG（JPEG 等）时框架的贴图工厂也会拒绝，两者都按「跳过并告警」处理。
        if (!PngHeader.TryReadSize(bytes, out var width, out var height))
        {
            Log.LogWarning($"Failed to decode image resource (PNG only): {uri.Value}");
            return new RexBinaryAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }

        if (ModRuntime.Assets is not { } assets)
        {
            Log.LogWarning($"Asset factory unavailable; image resource {uri.Value} is registered as binary.");
            return new RexBinaryAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }

        // 贴图解码是框架自己的 PNG 解码（引擎的 ImageConversion 不在本构建的互操作集里）。
        if (!assets.TryCreateTexture(bytes, out var texture))
        {
            Log.LogWarning($"Failed to decode image resource: {uri.Value}");
            return new RexBinaryAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }

        if (!assets.TryCreateSprite(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                DefaultPixelsPerUnit,
                out var sprite))
        {
            Log.LogWarning($"Failed to cut the image resource sprite: {uri.Value}");
            return new RexImageAsset(uri.Value, uri.PackageName, uri.Path, bytes, texture, null, width, height);
        }

        if (ModRuntime.Locator is not { } locator || !locator.TryRegisterSprite(uri.Value, sprite, out _))
            Log.LogWarning($"Failed to file the image resource: {uri.Value}");

        return new RexImageAsset(uri.Value, uri.PackageName, uri.Path, bytes, texture, sprite, width, height);
    }

    private static RexAsset CreateAudioAsset(RexUri uri, byte[] bytes)
    {
        // 容器解码是框架的 WavAudio：只读 PCM / IEEE float 的 RIFF/WAVE，其余容器按「跳过并告警」处理。
        if (!WavAudio.TryDecode(bytes, out var wav))
        {
            Log.LogWarning($"Failed to decode audio resource (RIFF/WAVE only): {uri.Value}");
            return new RexAudioAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }

        if (ModRuntime.Assets is not { } assets)
        {
            Log.LogWarning($"Asset factory unavailable; audio resource {uri.Value} has no clip.");
            return new RexAudioAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }

        if (!assets.TryCreateAudioClip(uri.Value, wav.Samples, wav.Channels, wav.SampleRate, out var clip))
        {
            Log.LogWarning($"Failed to create the audio resource clip: {uri.Value}");
            return new RexAudioAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }

        if (ModRuntime.Locator is not { } locator || !locator.TryRegisterAudioClip(uri.Value, clip, out _))
            Log.LogWarning($"Failed to file the audio resource: {uri.Value}");

        return new RexAudioAsset(uri.Value, uri.PackageName, uri.Path, bytes, clip);
    }

    private static void RegisterAsset(RexAsset asset)
    {
        if (_assets.ContainsKey(asset.Uri))
            Log.LogWarning($"Duplicate rex asset URI '{asset.Uri}' detected. Overwriting previous asset.");

        _assets[asset.Uri] = asset;
    }
}

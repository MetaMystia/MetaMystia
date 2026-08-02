using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MetaMystia.ResourceEx.Addressables;
using UnityEngine;

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

public sealed class RexImageAsset : RexAsset
{
    public RexImageAsset(string uri, string packageName, string path, byte[] bytes, Texture2D texture, Sprite sprite)
        : base(uri, packageName, path, bytes, RexAssetKind.Image)
    {
        Texture = texture;
        Sprite = sprite;
    }

    public Texture2D Texture { get; }
    public Sprite Sprite { get; }
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

public sealed class RexAudioAsset : RexAsset
{
    public RexAudioAsset(string uri, string packageName, string path, byte[] bytes, AudioClip clip = null)
        : base(uri, packageName, path, bytes, RexAssetKind.Audio)
    {
        Clip = clip;
    }

    public AudioClip Clip { get; }
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
    // path are exact-match. This stays in lockstep with RuntimeAddressables.KeyToGuid (MD5),
    // which is byte-sensitive — so a single source of truth across both registries.
    private static readonly Dictionary<string, RexAsset> _assets = new(StringComparer.Ordinal);

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
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                Log.LogWarning($"Failed to decode image resource: {uri.Value}");
                return new RexBinaryAsset(uri.Value, uri.PackageName, uri.Path, bytes);
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(uri.Path);
            texture.name = name;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                48f);

            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            RuntimeAddressables.RegisterSprite(uri.Value, sprite);
            return new RexImageAsset(uri.Value, uri.PackageName, uri.Path, bytes, texture, sprite);
        }
        catch (Exception ex)
        {
            UnityEngine.Object.DestroyImmediate(texture);
            Log.LogWarning($"Failed to create image resource {uri.Value}: {ex.Message}");
            return new RexBinaryAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }
    }

    private static RexAsset CreateAudioAsset(RexUri uri, byte[] bytes)
    {
        try
        {
            var clip = WavLoader.LoadFromBytes(bytes, uri.Value);
            RuntimeAddressables.Register(uri.Value, clip);
            return new RexAudioAsset(uri.Value, uri.PackageName, uri.Path, bytes, clip);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to register audio resource {uri.Value}: {ex.Message}");
            return new RexAudioAsset(uri.Value, uri.PackageName, uri.Path, bytes);
        }
    }

    private static void RegisterAsset(RexAsset asset)
    {
        if (_assets.ContainsKey(asset.Uri))
            Log.LogWarning($"Duplicate rex asset URI '{asset.Uri}' detected. Overwriting previous asset.");

        _assets[asset.Uri] = asset;
    }
}

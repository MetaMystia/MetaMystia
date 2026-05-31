using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using BepInEx;

using GameData.Core.Collections.CharacterUtility;

using Il2CppInterop.Runtime.InteropTypes.Arrays;

using UnityEngine;

namespace MetaMystia;

/// <summary>
/// 在线皮肤管理器：从皮肤服务器拉取 PNG 贴图，按约定布局解析为 CharacterSpriteSetCompact / CharacterSpriteSetFull，
/// 并维护内存与磁盘缓存。
///
/// PNG 布局（每格 64×64）：
/// Compact 576×256（每行 9 格）：
///   m00 m01 m02 e00 e10 e20 e30 e40 e50  (top)
///   m10 m11 m12 e01 e11 e21 e31 e41 e51
///   m20 m21 m22 e02 e12 e22 e32 e42 e52
///   m30 m31 m32 e03 e13 e23 e33 e43 e53  (bottom)
///
/// Full 960×256（每行 15 格，额外加上 Hair / Back）：
///   m00 m01 m02 e00 e10 e20 e30 e40 e50 h00 h01 h02 b00 b01 b02  (top)
///   m10 m11 m12 e01 e11 e21 e31 e41 e51 h10 h11 h12 b10 b11 b12
///   m20 m21 m22 e02 e12 e22 e32 e42 e52 h20 h21 h22 b20 b21 b22
///   m30 m31 m32 e03 e13 e23 e33 e43 e53 h30 h31 h32 b30 b31 b32  (bottom)
///
///   m{R}{C} 表示 Main (R=0..3, C=0..2)
///   e{R}{C} 表示 Eyes (R=0..5, C=0..3)
///   h{R}{C} 表示 Hair (R=0..3, C=0..2)
///   b{R}{C} 表示 Back (R=0..3, C=0..2)
/// </summary>
[AutoLog]
public static partial class NetSkinManager
{
    private const int TileSize = 64;
    private const int CompactWidth = 9 * TileSize;   // 576
    private const int CompactHeight = 4 * TileSize;  // 256
    private const int FullWidth = 15 * TileSize;     // 960
    private const int FullHeight = 4 * TileSize;     // 256

    private const int MainDirections = 4;
    private const int MainFrames = 3;
    private const int EyeDirections = 6;
    private const int EyeFrames = 4;
    private const int HairDirections = 4;
    private const int HairFrames = 3;
    private const int BackDirections = 4;
    private const int BackFrames = 3;

    // Keep disk cache write implementation available, but disable its entry by default.
    // Flip this back to true if local skin cache persistence is needed again.
    private static bool EnableDiskCacheWrite => false;

    private const long MaxDownloadBytes = 1 * 1024 * 1024; // 1 MB
    private static readonly Regex NameRegex = new(@"^[A-Za-z0-9_\-]{1,32}$", RegexOptions.Compiled);

    private static readonly Dictionary<string, CharacterSpriteSetCompact> _builtSkins = new();
    private static readonly HashSet<string> _inFlight = new();
    private static readonly ConcurrentDictionary<string, List<Action<bool>>> _callbacks = new();

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    private static string CacheDir =>
        Path.Combine(Paths.CachePath, "MetaMystia", "skins");

    private static string ServerUrl =>
        ConfigManager.SkinServerUrl?.Value?.TrimEnd('/') ?? "https://skin.metamystia.net";

    private static string ServerToken =>
        ConfigManager.SkinServerToken?.Value;

    private static HttpRequestMessage NewRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        var token = ServerToken;
        if (!string.IsNullOrEmpty(token))
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        return req;
    }

    /// <summary>
    /// 校验皮肤名是否合法（白名单：字母数字下划线短横线，长度 1..32）
    /// </summary>
    public static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && NameRegex.IsMatch(name);

    /// <summary>
    /// 立即从内存缓存中获取已构建的皮肤
    /// </summary>
    public static bool TryGet(string name, out CharacterSpriteSetCompact skin)
    {
        skin = null;
        if (string.IsNullOrEmpty(name)) return false;
        lock (_builtSkins)
        {
            return _builtSkins.TryGetValue(name, out skin) && skin != null;
        }
    }

    /// <summary>
    /// 请求拉取并构建皮肤（异步）。
    /// 已在内存缓存中：立即回调。
    /// 在磁盘缓存中：调度到主线程解析后回调。
    /// 否则：后台下载 → 可选写入磁盘 → 主线程解析后回调。
    /// </summary>
    /// <param name="name">皮肤名（必须通过 IsValidName 校验）</param>
    /// <param name="onComplete">完成回调，参数为是否成功</param>
    public static void RequestSkin(string name, Action<bool> onComplete = null)
    {
        if (!IsValidName(name))
        {
            Log.Warning($"NetSkin：皮肤名不合法 「{name}」");
            onComplete?.Invoke(false);
            return;
        }

        if (TryGet(name, out _))
        {
            onComplete?.Invoke(true);
            return;
        }

        // 注册回调
        var list = _callbacks.GetOrAdd(name, _ => new List<Action<bool>>());
        if (onComplete != null)
        {
            lock (list) list.Add(onComplete);
        }

        // 防重复并发
        lock (_inFlight)
        {
            if (_inFlight.Contains(name)) return;
            _inFlight.Add(name);
        }

        // 优先尝试磁盘缓存
        var cachedPath = GetCachePath(name);
        if (File.Exists(cachedPath))
        {
            Log.Info($"NetSkin：从磁盘缓存加载 「{name}」");
            PluginManager.Instance.RunOnMainThread(() =>
            {
                bool ok = TryParseAndRegister(name, File.ReadAllBytes(cachedPath));
                FinishRequest(name, ok);
            });
            // 后台使用 ETag 重验证；如果服务器返回新内容则重新解析 + 刷新
            _ = RevalidateAsync(name);
            return;
        }

        // 后台下载
        _ = DownloadAsync(name);
    }

    /// <summary>
    /// 后台走 ETag 条件请求检查服务端是否更新。未更新返回 304 时什么都不做；
    /// 返回 200 时可选覆写磁盘缓存，重新解析并刷新玩家。并发保护交给 _inFlight。
    /// </summary>
    private static async Task RevalidateAsync(string name)
    {
        // 调用点已拿到 _inFlight；这里在同一任务生命周期内完成，由 FinishRequest 释放
        try
        {
            var etag = ReadCachedETag(name);
            if (string.IsNullOrEmpty(etag)) return; // 没有 ETag 侧车，不走重验证（避免额外下载）

            var url = $"{ServerUrl}/skins/{name}.png";
            using var req = NewRequest(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("If-None-Match", etag);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                return; // 缓存仍是最新
            }
            if (!resp.IsSuccessStatusCode)
            {
                Log.Info($"NetSkin：重验证 「{name}」 返回 HTTP {(int)resp.StatusCode}，保留现有缓存");
                return;
            }

            var bytes = await ReadBoundedAsync(resp);
            if (bytes == null || !IsPngHeader(bytes))
            {
                if (bytes != null) Log.Warning($"NetSkin：重验证 「{name}」 响应不是合法 PNG，保留现有缓存");
                return;
            }

            if (!TryWriteDiskCache(name, bytes, resp.Headers.ETag?.Tag, "写入重验证后的缓存"))
                return;

            Log.Info($"NetSkin：服务端 「{name}」 已更新，重新加载");
            PluginManager.Instance.RunOnMainThread(() =>
            {
                if (TryParseAndRegister(name, bytes))
                    RefreshPlayersUsingSkin(name);
            });
        }
        catch (Exception e)
        {
            Log.Info($"NetSkin：重验证 「{name}」 异常，保留现有缓存：{e.Message}");
        }
    }

    private static async Task DownloadAsync(string name)
    {
        byte[] payload = null;
        string etag = null;
        try
        {
            var url = $"{ServerUrl}/skins/{name}.png";
            Log.Info($"NetSkin：正在从 {url} 下载 「{name}」");

            using var req = NewRequest(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Warning($"NetSkin：下载 「{name}」 失败，HTTP {(int)resp.StatusCode}");
            }
            else if (resp.Content.Headers.ContentLength is long len && len > MaxDownloadBytes)
            {
                Log.Warning($"NetSkin：下载 「{name}」 被拒绝，大小 {len} 字节超过上限 {MaxDownloadBytes}");
            }
            else
            {
                payload = await ReadBoundedAsync(resp);
                etag = resp.Headers.ETag?.Tag;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"NetSkin：下载 「{name}」 抛出异常：{e.Message}");
        }

        if (payload == null || !IsPngHeader(payload))
        {
            if (payload != null) Log.Warning($"NetSkin：「{name}」 不是合法的 PNG");
            FinishOnMainThread(name, false);
            return;
        }

        // 可选写入磁盘缓存（写文件不需要主线程）
        TryWriteDiskCache(name, payload, etag, "写入磁盘缓存");

        // 主线程解析
        PluginManager.Instance.RunOnMainThread(() =>
        {
            bool parsed = TryParseAndRegister(name, payload);
            FinishRequest(name, parsed);
        });
    }

    private static void FinishOnMainThread(string name, bool ok)
    {
        PluginManager.Instance.RunOnMainThread(() => FinishRequest(name, ok));
    }

    private static void FinishRequest(string name, bool ok)
    {
        lock (_inFlight) _inFlight.Remove(name);

        if (ok)
        {
            // 通知所有 NetSkinName == name 的玩家刷新立绘
            try { RefreshPlayersUsingSkin(name); }
            catch (Exception e) { Log.Warning($"NetSkin：刷新玩家失败：{e.Message}"); }
        }

        if (_callbacks.TryRemove(name, out var list))
        {
            lock (list)
            {
                foreach (var cb in list)
                {
                    try { cb(ok); }
                    catch (Exception e) { Log.Warning($"NetSkin：回调抛出异常：{e.Message}"); }
                }
            }
        }
    }

    private static void RefreshPlayersUsingSkin(string name)
    {
        if (PlayerManager.Local?.Skin?.NetSkinName == name)
            PlayerManager.Local.UpdateCharacterSprite();
        foreach (var peer in PlayerManager.Peers.Values)
        {
            if (peer?.Skin?.NetSkinName == name)
                peer.UpdateCharacterSprite();
        }
    }

    private static bool IsPngHeader(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 8) return false;
        return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
    }

    /// <summary>
    /// 主线程：将 PNG 字节解析为 CharacterSpriteSetCompact / CharacterSpriteSetFull 并加入内存缓存。
    /// </summary>
    private static bool TryParseAndRegister(string name, byte[] pngBytes)
    {
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (!ImageConversion.LoadImage(tex, pngBytes))
            {
                Log.Warning($"NetSkin：「{name}」 贴图加载失败");
                UnityEngine.Object.Destroy(tex);
                return false;
            }
            tex.name = $"NetSkin_{name}";

            // 按尺寸自动判别 Compact / Full
            CharacterSpriteSetCompact skin = null;
            string kind = null;
            if (tex.width == CompactWidth && tex.height == CompactHeight)
            {
                skin = BuildCompactFromTexture(name, tex);
                kind = "Compact";
            }
            else if (tex.width == FullWidth && tex.height == FullHeight)
            {
                skin = BuildFullFromTexture(name, tex);
                kind = "Full";
            }
            else
            {
                Log.Warning($"NetSkin：「{name}」 尺寸 {tex.width}×{tex.height} 不受支持 " +
                            $"（期望 Compact {CompactWidth}×{CompactHeight} 或 Full {FullWidth}×{FullHeight}）");
            }

            if (skin == null)
            {
                UnityEngine.Object.Destroy(tex);
                return false;
            }

            lock (_builtSkins) _builtSkins[name] = skin;
            Log.Info($"NetSkin：已注册 {kind} 皮肤 「{name}」");
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"NetSkin：解析 「{name}」 抛出异常：{e.Message}");
            return false;
        }
    }

    private static CharacterSpriteSetCompact BuildCompactFromTexture(string name, Texture2D tex)
    {
        var template = DataBaseCharacter.FallbackCompactPixel;
        if (template == null)
        {
            Log.Warning("NetSkin：FallbackCompactPixel 模板为空");
            return null;
        }

        var mainSprites = NewSpriteArray(template.MainSprite);
        var eyeSprites = NewSpriteArray(template.EyeSprite);
        if (mainSprites == null || eyeSprites == null) return null;

        SliceMain(name, tex, mainSprites, columnOffset: 0);
        SliceEyes(name, tex, eyeSprites, columnOffset: 3);

        var pixel = ScriptableObject.CreateInstance<CharacterSpriteSetCompact>();
        pixel.Initialize(
            mainSprites,
            template.DoNotUseEyeSprite,
            eyeSprites,
            template.HasPrebakedShadow,
            template.AnimationSpeedMultiplier,
            template.ExtraYOffset,
            template.IsHina,
            template.RotatePerTime,
            template.DoNotHaveStepVFX,
            template.MoveSpeedMultiplier,
            template.RemovableTrims,
            template.TrimSpritesDisplayFront,
            template.TrimSpritesDisplayBack,
            template.TrimFrontSpriteFrameSpeed,
            template.TrimBackSpriteFrameSpeed
        );
        pixel.name = $"NetSkin_{name}";
        pixel.hideFlags = HideFlags.HideAndDontSave;
        return pixel;
    }

    private static CharacterSpriteSetFull BuildFullFromTexture(string name, Texture2D tex)
    {
        var template = DataBaseCharacter.FallbackFullPixel;
        if (template == null)
        {
            Log.Warning("NetSkin：FallbackFullPixel 模板为空");
            return null;
        }

        var mainSprites = NewSpriteArray(template.MainSprite);
        var eyeSprites = NewSpriteArray(template.EyeSprite);
        var hairSprites = NewSpriteArray(template.HairSprite);
        var backSprites = NewSpriteArray(template.BackSprite);
        if (mainSprites == null || eyeSprites == null || hairSprites == null || backSprites == null) return null;

        SliceMain(name, tex, mainSprites, columnOffset: 0);
        SliceEyes(name, tex, eyeSprites, columnOffset: 3);
        SliceHairOrBack(name, tex, hairSprites, columnOffset: 9, tag: 'H');
        SliceHairOrBack(name, tex, backSprites, columnOffset: 12, tag: 'B');

        var pixel = ScriptableObject.CreateInstance<CharacterSpriteSetFull>();
        pixel.Initialize(
            mainSprites,
            template.DoNotUseEyeSprite,
            eyeSprites,
            hairSprites,
            backSprites,
            template.HasPrebakedShadow,
            template.AnimationSpeedMultiplier,
            template.ExtraYOffset,
            template.IsHina,
            template.RotatePerTime,
            template.DoNotHaveStepVFX,
            template.MoveSpeedMultiplier,
            template.RemovableTrims,
            template.TrimSpritesDisplayFront,
            template.TrimSpritesDisplayBack,
            template.TrimFrontSpriteFrameSpeed,
            template.TrimBackSpriteFrameSpeed
        );
        pixel.name = $"NetSkin_{name}";
        pixel.hideFlags = HideFlags.HideAndDontSave;
        return pixel;
    }

    /// <summary>
    /// Main 区：列 columnOffset..columnOffset+2，行 0..3，索引 = dir*MainFrames + frame
    /// </summary>
    private static void SliceMain(string name, Texture2D tex, Il2CppReferenceArray<Sprite> target, int columnOffset)
    {
        for (int dir = 0; dir < MainDirections; dir++)
        {
            for (int frame = 0; frame < MainFrames; frame++)
            {
                int idx = dir * MainFrames + frame;
                if (idx >= target.Length) break;
                int x = (columnOffset + frame) * TileSize;
                int y = (4 - 1 - dir) * TileSize; // Unity 纹理原点在左下
                target[idx] = SliceSprite(tex, x, y, $"{name}_M{dir}_{frame}");
            }
        }
    }

    /// <summary>
    /// Eyes 区：列 columnOffset..columnOffset+5（dir = col-columnOffset），行 0..3 即 frame
    /// </summary>
    private static void SliceEyes(string name, Texture2D tex, Il2CppReferenceArray<Sprite> target, int columnOffset)
    {
        for (int dir = 0; dir < EyeDirections; dir++)
        {
            for (int frame = 0; frame < EyeFrames; frame++)
            {
                int idx = dir * EyeFrames + frame;
                if (idx >= target.Length) break;
                int x = (columnOffset + dir) * TileSize;
                int y = (4 - 1 - frame) * TileSize;
                target[idx] = SliceSprite(tex, x, y, $"{name}_E{dir}_{frame}");
            }
        }
    }

    /// <summary>
    /// Hair / Back 区（与 Main 同样布局）4 dir × 3 frame。tag 仅用于命名精灵。
    /// </summary>
    private static void SliceHairOrBack(string name, Texture2D tex, Il2CppReferenceArray<Sprite> target, int columnOffset, char tag)
    {
        const int dirs = HairDirections;   // 与 Back 一致
        const int frames = HairFrames;
        for (int dir = 0; dir < dirs; dir++)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                int idx = dir * frames + frame;
                if (idx >= target.Length) break;
                int x = (columnOffset + frame) * TileSize;
                int y = (4 - 1 - dir) * TileSize;
                target[idx] = SliceSprite(tex, x, y, $"{name}_{tag}{dir}_{frame}");
            }
        }
    }

    private static Il2CppReferenceArray<Sprite> NewSpriteArray(
        Il2CppReferenceArray<Sprite> templateArray)
    {
        if (templateArray == null)
        {
            Log.Warning("NetSkin：模板精灵数组为空");
            return null;
        }
        // 沿用模板长度，避免与游戏 Initialize 内部假设不匹配
        var result = new Il2CppReferenceArray<Sprite>(templateArray.Length);
        for (int i = 0; i < templateArray.Length; i++)
            result[i] = templateArray[i];
        return result;
    }

    private static Sprite SliceSprite(Texture2D atlas, int x, int y, string name)
    {
        var sprite = Sprite.Create(
            atlas,
            new Rect(x, y, TileSize, TileSize),
            new Vector2(0.5f, 0f),
            48f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static string GetCachePath(string name) =>
        Path.Combine(CacheDir, $"{name}.png");

    private static string GetETagPath(string name) =>
        Path.Combine(CacheDir, $"{name}.etag");

    private static bool TryWriteDiskCache(string name, byte[] payload, string etag, string operation)
    {
        if (!EnableDiskCacheWrite) return true;

        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllBytes(GetCachePath(name), payload);
            WriteETag(name, etag);
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"NetSkin：{operation} 「{name}」 失败：{e.Message}");
            return false;
        }
    }

    private static string ReadCachedETag(string name)
    {
        try
        {
            var p = GetETagPath(name);
            if (!File.Exists(p)) return null;
            var v = File.ReadAllText(p).Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }
        catch { return null; }
    }

    private static void WriteETag(string name, string etag)
    {
        try
        {
            var p = GetETagPath(name);
            if (string.IsNullOrEmpty(etag))
            {
                if (File.Exists(p)) File.Delete(p);
            }
            else
            {
                File.WriteAllText(p, etag);
            }
        }
        catch (Exception e)
        {
            Log.Warning($"NetSkin：写入 ETag 侧车 「{name}」 失败：{e.Message}");
        }
    }

    /// <summary>
    /// 读取响应体，带上限保护。超限返回 null。
    /// </summary>
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage resp)
    {
        if (resp.Content.Headers.ContentLength is long len && len > MaxDownloadBytes) return null;
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        long total = 0;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxDownloadBytes) return null;
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// 删除某个皮肤的磁盘 + 内存缓存。下次请求会重新拉取。
    /// </summary>
    public static void Invalidate(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        lock (_builtSkins) _builtSkins.Remove(name);
        try
        {
            var p = GetCachePath(name);
            if (File.Exists(p)) File.Delete(p);
            var et = GetETagPath(name);
            if (File.Exists(et)) File.Delete(et);
        }
        catch (Exception e)
        {
            Log.Warning($"NetSkin：清理缓存 「{name}」 失败：{e.Message}");
        }
    }
}

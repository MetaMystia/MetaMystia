using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Mystia.Assets;
using Mystia.Imgui;
using Mystia.Numerics;

namespace MetaMystia;

/// <summary>
/// 在线皮肤管理器：从皮肤服务器拉取 PNG 贴图，按约定布局切成帧精灵，再交给框架造成游戏的角色像素集，
/// 并维护内存与磁盘缓存。
///
/// 贴图解码、尺寸查询、切精灵与精灵集构建都走框架的 <see cref="IAssetFactory"/>
/// （PNG 由框架解码：模组不再偷看 PNG 文件头，尺寸取自 <see cref="IAssetFactory.TryGetTextureSize"/>），
/// 帧精灵再按 key 登记进 <see cref="IAssetLocator"/>；<see cref="IAssetFactory.TryCreateCharacterSpriteSet"/>
/// 把帧集装成游戏的角色精灵集，套用交给 <c>IPresentationServices.ApplyCharacterSprite</c>（见 PlayerSkin）。
/// 缓存的 PNG 仍走 <see cref="IModStorage"/> 的缓存区。
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
    private const float TilePixelsPerUnit = 48f;
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

    private static readonly Dictionary<string, NetSkin?> _builtSkins = new();
    private static readonly HashSet<string> _inFlight = new();
    private static readonly ConcurrentDictionary<string, List<Action<bool>>> _callbacks = new();

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    // 皮肤缓存只给相对路径：读写都交给框架的模组缓存，绝对位置对模组隐藏。
    private const string CacheFolder = "skins";

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
    public static bool TryGet(string name, [NotNullWhen(true)] out NetSkin? skin)
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
    public static void RequestSkin(string name, Action<bool>? onComplete = null)
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
        if (CacheExists(GetCachePath(name)))
        {
            Log.Info($"NetSkin：从磁盘缓存加载 「{name}」");
            PluginManager.RunOnMainThread(() =>
            {
                bool ok = TryParseAndRegister(name, CacheReadBytes(GetCachePath(name)));
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
            if (bytes == null)
            {
                Log.Warning($"NetSkin：重验证 「{name}」 响应为空，保留现有缓存");
                return;
            }

            // 图像是否可用只由框架解码判定（模组不再校验 PNG 结构），因此先解析、解析通过才落盘。
            var freshETag = resp.Headers.ETag?.Tag;
            Log.Info($"NetSkin：服务端 「{name}」 已更新，重新加载");
            PluginManager.RunOnMainThread(() =>
            {
                if (!TryParseAndRegister(name, bytes))
                    return;
                TryWriteDiskCache(name, bytes, freshETag, "写入重验证后的缓存");
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

        if (payload == null)
        {
            Log.Warning($"NetSkin：下载 「{name}」 未取得内容");
            FinishOnMainThread(name, false);
            return;
        }

        // 主线程解析：贴图与帧精灵集都由框架构建，解析通过后才落到磁盘缓存（解不开的响应不落盘）。
        PluginManager.RunOnMainThread(() =>
        {
            bool parsed = TryParseAndRegister(name, payload);
            if (parsed) TryWriteDiskCache(name, payload, etag, "写入磁盘缓存");
            FinishRequest(name, parsed);
        });
    }

    private static void FinishOnMainThread(string name, bool ok)
    {
        PluginManager.RunOnMainThread(() => FinishRequest(name, ok));
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

    /// <summary>
    /// 主线程：把 PNG 字节交给框架解码、切成帧精灵，再用帧集造出游戏的角色精灵集，加入内存缓存。
    /// 尺寸取自贴图句柄（<see cref="IAssetFactory.TryGetTextureSize"/>），不再偷看 PNG 文件头。
    /// </summary>
    private static bool TryParseAndRegister(string name, byte[] pngBytes)
    {
        if (ModRuntime.Assets is not { } assets)
        {
            Log.Warning($"NetSkin：「{name}」 资产工厂不可用");
            return false;
        }

        if (!assets.TryCreateTexture(pngBytes, out var texture))
        {
            Log.Warning($"NetSkin：「{name}」 贴图加载失败（框架只解码 PNG）");
            return false;
        }

        if (!assets.TryGetTextureSize(texture, out var width, out var height))
        {
            Log.Warning($"NetSkin：「{name}」 无法向框架查询贴图尺寸");
            return false;
        }

        // 按尺寸自动判别 Compact / Full
        bool? isFull = (width, height) switch
        {
            (CompactWidth, CompactHeight) => false,
            (FullWidth, FullHeight) => true,
            _ => null,
        };
        if (isFull is null)
        {
            Log.Warning($"NetSkin：「{name}」 尺寸 {width}×{height} 不受支持 " +
                        $"（期望 Compact {CompactWidth}×{CompactHeight} 或 Full {FullWidth}×{FullHeight}）");
            return false;
        }

        var main = Slice(assets, texture, name, "main", 0, MainFrames, MainDirections, directionsAlongColumns: false);
        var eyes = Slice(assets, texture, name, "eyes", 3, EyeFrames, EyeDirections, directionsAlongColumns: true);
        List<SpriteHandle>? hair = isFull == true ? Slice(assets, texture, name, "hair", 9, HairFrames, HairDirections, directionsAlongColumns: false) : [];
        List<SpriteHandle>? back = isFull == true ? Slice(assets, texture, name, "back", 12, BackFrames, BackDirections, directionsAlongColumns: false) : [];
        if (main is null || eyes is null || hair is null || back is null)
        {
            Log.Warning($"NetSkin：「{name}」 切图失败");
            return false;
        }

        // 帧精灵由框架装成游戏的角色像素集；套用由 PlayerSkin 走 IPresentationServices.ApplyCharacterSprite。
        var frames = new CharacterSpriteSetFrames(main.ToArray(), eyes.ToArray(), hair.ToArray(), back.ToArray());
        var kind = isFull == true ? CharacterSpriteSetKind.Full : CharacterSpriteSetKind.Compact;
        if (!assets.TryCreateCharacterSpriteSet(kind, frames, CharacterSpriteSetStyle.Default, out var set))
        {
            Log.Warning($"NetSkin：「{name}」 精灵集构建失败（帧集不满足框架要求）");
            return false;
        }

        lock (_builtSkins) _builtSkins[name] = new NetSkin(name, isFull == true, main.ToArray(), eyes.ToArray(), hair.ToArray(), back.ToArray(), set);
        Log.Info($"NetSkin：已注册 {(isFull == true ? "Full" : "Compact")} 皮肤 「{name}」");
        return true;
    }

    /// <summary>
    /// 切一组 64×64 帧精灵并登记进资产管线。索引与迁移前一致：<c>dir*frames + frame</c>；
    /// <paramref name="directionsAlongColumns"/> 为真时方向沿列、帧沿行（Eyes 区），否则帧沿列、方向沿行（Main／Hair／Back 区）。
    /// 任一切图被拒时返回 null。
    /// </summary>
    private static List<SpriteHandle>? Slice(
        IAssetFactory assets, TextureHandle texture, string name, string group,
        int columnOffset, int frames, int directions, bool directionsAlongColumns)
    {
        if (ModRuntime.Locator is not { } locator)
        {
            Log.Warning($"NetSkin：「{name}」 资产登记表不可用");
            return null;
        }

        var sprites = new List<SpriteHandle>(frames * directions);
        for (int dir = 0; dir < directions; dir++)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                int x = (columnOffset + (directionsAlongColumns ? dir : frame)) * TileSize;
                int y = (4 - 1 - (directionsAlongColumns ? frame : dir)) * TileSize; // 贴图原点在左下
                var index = dir * frames + frame;
                if (!assets.TryCreateSprite(texture, new Rect(x, y, TileSize, TileSize), new Vector2(0.5f, 0f), TilePixelsPerUnit, out var sprite)
                    || !locator.TryRegisterSprite(FrameKey(name, group, index), sprite, out _))
                {
                    Log.Warning($"NetSkin：「{name}」 切图失败：{group}[{index}]");
                    return null;
                }

                sprites.Add(sprite);
            }
        }

        return sprites;
    }

    /// <summary>帧精灵在资产管线里的 key；前缀按模组命名空间，避免与其他 mod 冲突。</summary>
    private static string FrameKey(string name, string group, int index) => $"{ModRuntime.Id}/skin/{name}/{group}/{index}";

    /// <summary>解绑一张皮肤的全部帧精灵（内存缓存被丢弃时调用）。</summary>
    private static void UnregisterFrames(string name)
    {
        if (ModRuntime.Locator is not { } locator)
            return;

        foreach (var (group, count) in Groups)
            for (var index = 0; index < count; index++)
                locator.Unregister(FrameKey(name, group, index));
    }

    // 各组帧数，取自上方布局说明；Compact 没有 Hair／Back，解绑时多问几个 key 无副作用。
    private static readonly (string Group, int Count)[] Groups =
    [
        ("main", MainDirections * MainFrames),
        ("eyes", EyeDirections * EyeFrames),
        ("hair", HairDirections * HairFrames),
        ("back", BackDirections * BackFrames),
    ];

    private static string GetCachePath(string name) => $"{CacheFolder}/{name}.png";

    private static string GetETagPath(string name) => $"{CacheFolder}/{name}.etag";

    /// <summary>
    /// <see cref="ModRuntime.Storage"/> 未就绪（早期初始化）时退回模组目录下的同名子目录。
    /// </summary>
    private static string FallbackPath(string relativePath) =>
        Path.Combine(ModRuntime.Directory, relativePath);

    private static bool CacheExists(string relativePath) =>
        ModRuntime.Storage is { } storage ? storage.Exists(relativePath) : File.Exists(FallbackPath(relativePath));

    private static byte[] CacheReadBytes(string relativePath)
    {
        if (ModRuntime.Storage is not { } storage)
            return File.ReadAllBytes(FallbackPath(relativePath));

        if (!storage.TryOpenRead(relativePath, out var stream))
            throw new FileNotFoundException($"缓存文件不存在：{relativePath}");

        using (stream)
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }

    private static string CacheReadText(string relativePath)
    {
        if (ModRuntime.Storage is not { } storage)
            return File.ReadAllText(FallbackPath(relativePath));

        if (!storage.TryOpenRead(relativePath, out var stream))
            throw new FileNotFoundException($"缓存文件不存在：{relativePath}");

        using (stream)
        using (var reader = new StreamReader(stream))
            return reader.ReadToEnd();
    }

    private static void CacheWriteBytes(string relativePath, byte[] bytes)
    {
        if (ModRuntime.Storage is { } storage)
        {
            if (!storage.TryOpenWrite(relativePath, out var stream))
                throw new IOException($"无法写入缓存文件：{relativePath}");
            using (stream)
                stream.Write(bytes, 0, bytes.Length);
            return;
        }

        var path = FallbackPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void CacheWriteText(string relativePath, string text)
    {
        if (ModRuntime.Storage is { } storage)
        {
            if (!storage.TryOpenWrite(relativePath, out var stream))
                throw new IOException($"无法写入缓存文件：{relativePath}");
            using (stream)
            using (var writer = new StreamWriter(stream))
                writer.Write(text);
            return;
        }

        var path = FallbackPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void CacheDelete(string relativePath)
    {
        if (ModRuntime.Storage is { } storage)
        {
            storage.TryDelete(relativePath);
            return;
        }

        var path = FallbackPath(relativePath);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static bool TryWriteDiskCache(string name, byte[] payload, string etag, string operation)
    {
        if (!EnableDiskCacheWrite) return true;

        try
        {
            CacheWriteBytes(GetCachePath(name), payload);
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
            if (!CacheExists(GetETagPath(name))) return null;
            var v = CacheReadText(GetETagPath(name)).Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }
        catch { return null; }
    }

    private static void WriteETag(string name, string etag)
    {
        try
        {
            if (string.IsNullOrEmpty(etag))
            {
                if (CacheExists(GetETagPath(name))) CacheDelete(GetETagPath(name));
            }
            else
            {
                CacheWriteText(GetETagPath(name), etag);
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
        bool cached;
        lock (_builtSkins) cached = _builtSkins.Remove(name);
        if (cached) UnregisterFrames(name);
        try
        {
            if (CacheExists(GetCachePath(name))) CacheDelete(GetCachePath(name));
            if (CacheExists(GetETagPath(name))) CacheDelete(GetETagPath(name));
        }
        catch (Exception e)
        {
            Log.Warning($"NetSkin：清理缓存 「{name}」 失败：{e.Message}");
        }
    }
}

/// <summary>
/// 一张已构建的线上皮肤：布局判别结果、四组帧精灵句柄与框架造好的角色像素集。
/// 索引与游戏像素集一致（Main／Hair／Back 为 dir*3 + frame，Eyes 为 dir*4 + frame）。Compact 布局没有
/// Hair／Back，两者为空表。
/// </summary>
public sealed class NetSkin
{
    /// <summary>旋转覆盖的旋转周期（秒），与原 <c>CloneWithRotationOverride(..., 0.15f)</c> 一致。</summary>
    internal const float RotatePerTimeSeconds = 0.15f;

    private readonly SpriteHandle[] _main;
    private readonly SpriteHandle[] _eyes;
    private readonly SpriteHandle[] _hair;
    private readonly SpriteHandle[] _back;

    internal NetSkin(
        string name,
        bool isFull,
        SpriteHandle[] main,
        SpriteHandle[] eyes,
        SpriteHandle[] hair,
        SpriteHandle[] back,
        CharacterSpriteSetHandle? set)
    {
        Name = name;
        IsFull = isFull;
        _main = main;
        _eyes = eyes;
        _hair = hair;
        _back = back;
        DefaultSet = set;
    }

    public string Name { get; }

    public bool IsFull { get; }

    public IReadOnlyList<SpriteHandle> Main => _main;

    public IReadOnlyList<SpriteHandle> Eyes => _eyes;

    public IReadOnlyList<SpriteHandle> Hair => _hair;

    public IReadOnlyList<SpriteHandle> Back => _back;

    /// <summary>不带任何风格的精灵集（<see cref="CharacterSpriteSetStyle.Default"/>），构建时由框架造好。</summary>
    internal CharacterSpriteSetHandle? DefaultSet { get; }

    /// <summary>
    /// 取这套帧的精灵集。<paramref name="rotateOverride"/> 为空时就是 <see cref="DefaultSet"/>；
    /// 非空时按原实现的语义用 <see cref="CharacterSpriteSetStyle"/> 重建一张带旋转覆盖的集
    /// （<c>IsHina = 覆盖值</c>，周期 <see cref="RotatePerTimeSeconds"/>）。
    /// </summary>
    internal bool TryCreateSet(bool? rotateOverride, [NotNullWhen(true)] out CharacterSpriteSetHandle? set)
    {
        if (rotateOverride is null)
        {
            set = DefaultSet;
            return set is not null;
        }

        set = null;
        if (ModRuntime.Assets is not { } assets)
            return false;

        var style = new CharacterSpriteSetStyle
        {
            IsHina = rotateOverride.Value,
            RotatePerTime = RotatePerTimeSeconds,
        };
        var kind = IsFull ? CharacterSpriteSetKind.Full : CharacterSpriteSetKind.Compact;
        var frames = new CharacterSpriteSetFrames(_main, _eyes, _hair, _back);
        return assets.TryCreateCharacterSpriteSet(kind, frames, style, out set);
    }
}

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Mystia.Assets;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 服装领域注册器：持有资源包声明的服装配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// 运行时立绘（<c>IPortraitProvider</c>）沿用同一份配置按 <c>rex://</c> URI 取精灵句柄。
/// </summary>
[AutoLog]
public static partial class ClothRegistry
{
    private static readonly Dictionary<int, ClothConfig> ClothConfigs = new();

    // Cloth portrait cache: clothId -> sprite handle (loaded lazily or during preload)
    private static readonly Dictionary<int, SpriteHandle> _clothPortraitCache = new();

    internal static IEnumerable<ClothConfig> Configs => ClothConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.clothes == null) return;

        foreach (var clothConfig in config.clothes)
        {
            ClothConfigs[clothConfig.id] = clothConfig;
            Log.LogInfo($"[{packageName}] Loaded config for cloth {clothConfig.name} ({clothConfig.id})");
        }
    }

    /// <summary>
    /// 判断一个服装ID是否由 ResourceEx 注册
    /// </summary>
    public static bool IsResourceExCloth(int clothId) => ClothConfigs.ContainsKey(clothId);

    /// <summary>
    /// 取 ResourceEx 注册的服装立绘句柄（用于 <c>IPortraitProvider</c> 中动态替换立绘）。
    /// </summary>
    public static bool TryGetClothPortrait(int clothId, [NotNullWhen(true)] out SpriteHandle? portrait)
    {
        portrait = null;
        if (_clothPortraitCache.TryGetValue(clothId, out var cached))
        {
            portrait = cached;
            return cached is not null;
        }

        if (!ClothConfigs.TryGetValue(clothId, out var config) || string.IsNullOrEmpty(config.portraitPath))
            return false;

        RexAssetRegistry.TryGetSprite(config.portraitPath, out portrait);
        if (portrait is null)
            return false;

        _clothPortraitCache[clothId] = portrait;
        return true;
    }
}

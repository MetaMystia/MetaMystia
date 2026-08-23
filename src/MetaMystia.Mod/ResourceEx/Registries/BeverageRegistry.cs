using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 饮料领域注册器：持有饮料配置，负责注册与语言注册。
/// </summary>
[AutoLog]
public static partial class BeverageRegistry
{
    private static readonly Dictionary<int, BeverageConfig> BeverageConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.beverages == null) return;

        foreach (var beverageConfig in config.beverages)
        {
            BeverageConfigs[beverageConfig.id] = beverageConfig;
            Log.LogInfo($"[{packageName}] Loaded config for beverage {beverageConfig.name} ({beverageConfig.id})");
        }
    }

    internal static void RegisterAllBeverageLanguages()
    {
        BeverageConfigs.Values.ToList().ForEach(RegisterBeverageLanguage);
    }

    private static void RegisterBeverageLanguage(BeverageConfig config)
    {
        RexAssetRegistry.TryGetSprite(config.spritePath, out var sprite);
        var lang = config.ToBeverageLanguage(sprite);
        DataBaseLanguage.Beverages[config.id] = lang; // Beverages 是 private 的，不能用 TryAdd
        Log.Info($"Registered language for beverage {config.id}: {config.name}");
    }

    internal static void RegisterAllBeverages()
    {
        BeverageConfigs.Values.ToList().ForEach(RegisterBeverage);
    }

    private static void RegisterBeverage(BeverageConfig config)
    {
        var beverage = config.ToBeverage();
        var success = DataBaseCore.Beverages.TryAdd(beverage.Id, beverage);
        var mappingSuccess = DataBaseCore.BeveragesMapping.TryAdd(config.id, "ResourceEx");
        Log.Info($"Registered beverage object {config.id}: {config.name}, success={success}, mappingSuccess={mappingSuccess}");
    }
}

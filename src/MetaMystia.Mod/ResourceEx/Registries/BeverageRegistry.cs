using System.Collections.Generic;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 饮料领域注册器：持有资源包声明的饮料配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// </summary>
[AutoLog]
public static partial class BeverageRegistry
{
    private static readonly Dictionary<int, BeverageConfig> BeverageConfigs = new();

    internal static IEnumerable<BeverageConfig> Configs => BeverageConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.beverages == null) return;

        foreach (var beverageConfig in config.beverages)
        {
            BeverageConfigs[beverageConfig.id] = beverageConfig;
            Log.LogInfo($"[{packageName}] Loaded config for beverage {beverageConfig.name} ({beverageConfig.id})");
        }
    }
}

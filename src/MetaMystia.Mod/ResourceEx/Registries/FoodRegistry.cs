using System.Collections.Generic;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 食物领域注册器：持有资源包声明的食物配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// </summary>
[AutoLog]
public static partial class FoodRegistry
{
    private static readonly Dictionary<int, FoodConfig> FoodConfigs = new();

    internal static IEnumerable<FoodConfig> Configs => FoodConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.foods == null) return;

        foreach (var foodConfig in config.foods)
        {
            FoodConfigs[foodConfig.id] = foodConfig;
            Log.LogInfo($"[{packageName}] Loaded config for food {foodConfig.name} ({foodConfig.id})");
        }
    }
}

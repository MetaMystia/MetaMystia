using System.Collections.Generic;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 食材领域注册器：持有资源包声明的食材配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// </summary>
[AutoLog]
public static partial class IngredientRegistry
{
    private static readonly Dictionary<int, IngredientConfig> IngredientConfigs = new();

    internal static IEnumerable<IngredientConfig> Configs => IngredientConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.ingredients == null) return;

        foreach (var ingredientConfig in config.ingredients)
        {
            IngredientConfigs[ingredientConfig.id] = ingredientConfig;
            Log.LogInfo($"[{packageName}] Loaded config for ingredient {ingredientConfig.id}");
        }
    }
}

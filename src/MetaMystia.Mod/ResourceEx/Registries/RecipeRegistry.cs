using System.Collections.Generic;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 配方领域注册器：持有资源包声明的配方配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// </summary>
[AutoLog]
public static partial class RecipeRegistry
{
    private static readonly Dictionary<int, RecipeConfig> RecipeConfigs = new();

    internal static IEnumerable<RecipeConfig> Configs => RecipeConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.recipes == null) return;

        foreach (var recipeConfig in config.recipes)
        {
            RecipeConfigs[recipeConfig.id] = recipeConfig;
            Log.LogInfo($"[{packageName}] Loaded config for recipe {recipeConfig.id}");
        }
    }
}

using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;

using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 配方领域注册器：持有配方配置，负责注册。
/// </summary>
[AutoLog]
public static partial class RecipeRegistry
{
    private static readonly Dictionary<int, RecipeConfig> RecipeConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.recipes == null) return;

        foreach (var recipeConfig in config.recipes)
        {
            RecipeConfigs[recipeConfig.id] = recipeConfig;
            Log.LogInfo($"[{packageName}] Loaded config for recipe {recipeConfig.id}");
        }
    }

    internal static void RegisterAllRecipes() => RecipeConfigs.Values.ToList().ForEach(RegisterRecipe);

    private static void RegisterRecipe(RecipeConfig config)
    {
        var recipe = config.ToRecipe();
        var success = DataBaseCore.Recipes.TryAdd(config.id, recipe);
        var mappingSuccess = DataBaseCore.RecipesMapping.TryAdd(config.id, "ResourceEx");
        Log.Info($"Registered Recipe ID {config.id} for Food ID {config.foodId}: Success: {success}, Mapping Success: {mappingSuccess}");
    }
}

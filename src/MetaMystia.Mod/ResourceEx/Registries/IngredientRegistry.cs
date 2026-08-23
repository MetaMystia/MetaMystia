using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 食材领域注册器：持有食材配置，负责注册与语言注册。
/// </summary>
[AutoLog]
public static partial class IngredientRegistry
{
    private static readonly Dictionary<int, IngredientConfig> IngredientConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.ingredients == null) return;

        foreach (var ingredientConfig in config.ingredients)
        {
            IngredientConfigs[ingredientConfig.id] = ingredientConfig;
            Log.LogInfo($"[{packageName}] Loaded config for ingredient {ingredientConfig.id}");
        }
    }

    internal static void RegisterAllIngredientLanguages()
    {
        IngredientConfigs.Values.ToList().ForEach(RegisterIngredientLanguage);
    }

    private static void RegisterIngredientLanguage(IngredientConfig config)
    {
        RexAssetRegistry.TryGetSprite(config.spritePath, out var sprite);
        var lang = config.ToIngredientLanguage(sprite);
        DataBaseLanguage.Ingredients[config.id] = lang; // Ingredients 是 private 的，不能用 TryAdd
        Log.Info($"Registered language for ingredient {config.id}: {config.name}");
    }

    internal static void RegisterAllIngredients()
    {
        IngredientConfigs.Values.ToList().ForEach(RegisterIngredient);
    }

    private static void RegisterIngredient(IngredientConfig config)
    {
        var ingredient = config.ToIngredient();
        var success = DataBaseCore.Ingredients.TryAdd(ingredient.Id, ingredient);
        Log.Info($"Registered ingredient object {config.id}: {config.name}, success={success}");
    }
}

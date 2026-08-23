using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 食物领域注册器：持有食物配置，负责注册与语言注册。
/// </summary>
[AutoLog]
public static partial class FoodRegistry
{
    private static readonly Dictionary<int, FoodConfig> FoodConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.foods == null) return;

        foreach (var foodConfig in config.foods)
        {
            FoodConfigs[foodConfig.id] = foodConfig;
            Log.LogInfo($"[{packageName}] Loaded config for food {foodConfig.name} ({foodConfig.id})");
        }
    }

    internal static void RegisterAllFoods() => FoodConfigs.Values.ToList().ForEach(RegisterFood);

    private static void RegisterFood(FoodConfig config)
    {
        var food = config.ToFood();
        var success = DataBaseCore.Foods.TryAdd(config.id, food);
        var mappingSuccess = DataBaseCore.FoodsMapping.TryAdd(config.id, "ResourceEx");
        Log.Info($"Registered Food ID {config.id} ({config.name}): Success: {success}, Mapping Success: {mappingSuccess}");
    }

    internal static void RegisterAllFoodLanguages() => FoodConfigs.Values.ToList().ForEach(RegisterFoodLanguage);

    private static void RegisterFoodLanguage(FoodConfig config)
    {
        RexAssetRegistry.TryGetSprite(config.spritePath, out var sprite);
        var lang = config.ToFoodLanguage(sprite);
        DataBaseLanguage.Foods[config.id] = lang;
        Log.Info($"Registered Food Language ID {config.id} ({config.name})");
    }
}

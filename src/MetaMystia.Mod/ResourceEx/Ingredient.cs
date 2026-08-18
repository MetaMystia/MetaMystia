using System.Linq;
using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia;

public static partial class ResourceExManager
{
    private static void RegisterAllIngredientLanguages()
    {
        IngredientConfigs.Values.ToList().ForEach(RegisterIngredientLanguage);
    }

    private static void RegisterIngredientLanguage(IngredientConfig config)
    {
        TryGetSprite(config.spritePath, out var sprite);
        var lang = config.ToIngredientLanguage(sprite);
        DataBaseLanguage.Ingredients[config.id] = lang; // Ingredients 是 private 的，不能用 TryAdd
        Log.Info($"Registered language for ingredient {config.id}: {config.name}");
    }

    private static void RegisterAllIngredients()
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

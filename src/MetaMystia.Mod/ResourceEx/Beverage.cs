using System.Linq;
using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia;

public static partial class ResourceExManager
{
    private static void RegisterAllBeverageLanguages()
    {
        BeverageConfigs.Values.ToList().ForEach(RegisterBeverageLanguage);
    }

    private static void RegisterBeverageLanguage(BeverageConfig config)
    {
        TryGetSprite(config.spritePath, out var sprite);
        var lang = config.ToBeverageLanguage(sprite);
        DataBaseLanguage.Beverages[config.id] = lang; // Beverages 是 private 的，不能用 TryAdd
        Log.Info($"Registered language for beverage {config.id}: {config.name}");
    }

    private static void RegisterAllBeverages()
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

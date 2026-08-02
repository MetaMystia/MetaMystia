using System.Linq;

using DEYU.Utils;
using GameData.Core.Collections;
using Il2CppSystem.IO;

namespace MetaMystia.Network;

internal static class SellableFood
{
    public static Sellable ToSellable(this SellableFoodData sellableFood)
    {
        if (sellableFood.Type == WireSellableType.Beverage)
            return sellableFood.Id.AsNewBeverage();

        var food = sellableFood.Id.AsNewFood();
        food.level = sellableFood.Level;
        food.modifier = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(sellableFood.ModifierIds);
        food.additiveTags = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var tag in sellableFood.AdditiveTags)
            food.additiveTags.Add(tag);

        var cooker = CookManager.GetCookerByCookerId(sellableFood.CookId);
        var guid = Il2CppSystem.Guid.NewGuid();
        food.RunTimeGUID = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(guid);

        if (cooker != null && cooker?.Id != null)
        {
            var result = NightScene.CookingUtility.CookSystemManager.Instance?.registeredSellables.TryAdd(
                food.RunTimeGUID.Unbox<Il2CppSystem.Guid>(),
                cooker);
            Plugin.Instance?.Log.LogInfo(
                $"registeredSellables tryadd {food.RunTimeGUID.Unbox<Il2CppSystem.Guid>()} => {cooker?.Id}, result {result}");
        }

        return food;
    }

    public static SellableFoodData FromSellable(Sellable sellable)
    {
        if (sellable == null) return null;

        if (sellable.Type == Sellable.SellableType.Beverage)
        {
            return new SellableFoodData
            {
                Id = sellable.Id,
                Type = WireSellableType.Beverage
            };
        }

        var result = new SellableFoodData
        {
            Type = WireSellableType.Food,
            Id = sellable.Id,
            Level = sellable.level,
            ModifierIds = sellable.modifier,
            AdditiveTags = sellable.additiveTags.ToArray()
        };
        if (NightScene.CookingUtility.CookSystemManager.Instance?.GetCooker(sellable, out var cooker) is true)
        {
            Plugin.Instance?.Log.LogInfo($"GetCooker id {cooker.Id}");
            result.CookId = cooker.Id;
        }

        return result;
    }

    public static Sellable GetFromLocal(this SellableFoodData sellableFood)
    {
        var storedFoods = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.GetStoredFoods();
        var matchingFood = from food in storedFoods.ToArray()
                           where food.Id == sellableFood.Id &&
                                 food.level == sellableFood.Level &&
                                 food.modifier.SequenceEqual(sellableFood.ModifierIds) &&
                                 food.additiveTags.ToArray().SequenceEqual(sellableFood.AdditiveTags.ToArray())
                           select food;

        return matchingFood.FirstOrDefault();
    }
}

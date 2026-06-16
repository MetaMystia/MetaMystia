using System.Linq;
using GameData.Core.Collections;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Data;
using NightScene.CookingUtility;

// ReSharper disable once CheckNamespace
namespace MetaMystia;

public static class SellableExtensions
{
    public static SellableFoodData ToSellableFoodData(this Sellable sellable)
    {
        if (sellable == null) return null;

        if (sellable.Type == Sellable.SellableType.Beverage)
        {
            return new SellableFoodData
            {
                Id = sellable.Id,
                Type = Protocol.Enums.SellableType.Beverage
            };
        }

        int cookId = 0;
        if (CookSystemManager.Instance?.GetCooker(sellable, out var cooker) is true)
        {
            cookId = cooker.Id;
        }

        return new SellableFoodData
        {
            Type = Protocol.Enums.SellableType.Food,
            Id = sellable.Id,
            Level = sellable.level,
            ModifierIds = sellable.modifier is { } mod ? mod.ToArray() : [],
            AdditiveTags = sellable.additiveTags is { } tags ? tags.ToArray() : [],
            CookId = cookId
        };
    }

    public static bool ContentEquals(this SellableFoodData a, SellableFoodData b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Type == b.Type
            && a.Id == b.Id
            && a.Level == b.Level
            && a.CookId == b.CookId
            && (a.ModifierIds ?? []).SequenceEqual(b.ModifierIds ?? [])
            && (a.AdditiveTags ?? []).SequenceEqual(b.AdditiveTags ?? []);
    }

    public static Sellable ToGameSellable(this SellableFoodData data)
    {
        if (data == null) return null;

        if (data.Type == Protocol.Enums.SellableType.Beverage)
        {
            return data.Id.AsNewBeverage();
        }

        var food = data.Id.AsNewFood();
        food.level = data.Level;
        food.modifier = new Il2CppStructArray<int>(data.ModifierIds ?? []);
        food.additiveTags = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var tag in data.AdditiveTags ?? [])
        {
            food.additiveTags.Add(tag);
        }

        var cooker = CookManager.GetCookerByCookerId(data.CookId);
        var guid = Il2CppSystem.Guid.NewGuid();
        food.RunTimeGUID = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(guid);

        if (cooker != null && cooker?.Id != null)
        {
            CookSystemManager.Instance?.registeredSellables.TryAdd(
                food.RunTimeGUID.Unbox<Il2CppSystem.Guid>(), cooker);
        }

        return food;
    }
}
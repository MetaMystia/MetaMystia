using System;
using System.Linq;
using DEYU.Utils;
using GameData.Core.Collections;
using Il2CppSystem.IO;
using MemoryPack;

namespace MetaMystia.Network;


[MemoryPackable]
[AutoLog]
public partial class SellableFood
{
    public Sellable.SellableType Type { get; set; }
    public int Id { get; set; }
    public int Level { get; set; }
    public int[] ModifierIds { get; set; } = []; // 附加原料
    public int[] AdditiveTags { get; set; } = [];
    public int CookId { get; set; }

    public Sellable ToSellable()
    {
        if (Type == Sellable.SellableType.Beverage)
        {
            return Id.AsNewBeverage();
        }
        
        var food = Id.AsNewFood();
        food.level = Level;
        food.modifier = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(ModifierIds);
        food.additiveTags = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var tag in AdditiveTags)
        {
            food.additiveTags.Add(tag);
        }
        var cooker = CookManager.GetCookerByCookerId(CookId);
        var guid = Il2CppSystem.Guid.NewGuid();
        food.RunTimeGUID = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(guid);

        if (cooker != null && cooker?.Id != null)
        {
            var b = NightScene.CookingUtility.CookSystemManager.Instance?.registeredSellables.TryAdd(food.RunTimeGUID.Unbox<Il2CppSystem.Guid>(), cooker);
            Log.Info($"registeredSellables tryadd {food.RunTimeGUID.Unbox<Il2CppSystem.Guid>()} => {cooker?.Id}, result {b}");
        }
        return food;
    }
    public static SellableFood FromSellable(Sellable sellable)
    {
        if (sellable == null) return null;
        
        if (sellable.Type == Sellable.SellableType.Beverage)
        {
            return new SellableFood()
            {
                Id = sellable.Id,
                Type = Sellable.SellableType.Beverage
            };
        }
        
        var res = new SellableFood
        {
            Type = Sellable.SellableType.Food,
            Id = sellable.Id,
            Level = sellable.level,
            ModifierIds = sellable.modifier,
            AdditiveTags = sellable.additiveTags.ToArray()
        };
        if (NightScene.CookingUtility.CookSystemManager.Instance?.GetCooker(sellable, out var cooker) is true)
        {
            Log.Info($"GetCooker id {cooker.Id}");
            res.CookId = cooker.Id;
        }
        return res;
    }

    /// <summary>
    /// 按内容比较两个 <see cref="SellableFood"/> 是否相等（用于联机冲突仲裁，不放入字典/集合）。
    /// </summary>
    public static bool ContentEquals(SellableFood a, SellableFood b)
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

    public Sellable GetFromLocal()
    {
        var storedFoods = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.GetStoredFoods();
        var matchingFood = from food in storedFoods.ToArray()
                           where food.Id == Id &&
                                 food.level == Level &&
                                 food.modifier.SequenceEqual(ModifierIds) &&
                                 food.additiveTags.ToArray().SequenceEqual(AdditiveTags.ToArray())
                           select food;

        return matchingFood.FirstOrDefault();
    }
}

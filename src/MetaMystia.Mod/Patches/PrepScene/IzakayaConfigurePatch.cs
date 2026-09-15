using System.Globalization;

using HarmonyLib;
using UnityEngine;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.NightSceneUtility.IzakayaConfigure))]
[AutoLog]
public partial class IzakayaConfigurePatch
{
    [HarmonyPatch(nameof(IzakayaConfigure.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix() => ApplyConfiguredFlowRate();

    [HarmonyPatch(nameof(IzakayaConfigure.UpdateValue))]
    [HarmonyPostfix]
    public static void UpdateValue_Postfix() => ApplyConfiguredFlowRate();

    private static void ApplyConfiguredFlowRate()
    {
        if (GameSession.IsRoomClient) return;

        float rate = ConfigManager.CheatFlowRate.Value;
        if (rate == 0f || rate == 1f || float.IsNaN(rate) || rate < 0f || rate >= 16f) return;

        var configure = IzakayaConfigure.Instance;
        if (configure == null) return;

        var normalInterval = configure.NormalGuestInterval;
        configure.NormalGuestInterval = new Vector2(normalInterval.x / rate, normalInterval.y / rate);
        configure.SpecialGuestGachaInterval /= rate;
        string rateText = rate.ToString("0.###", CultureInfo.InvariantCulture);
        Log.LogInfo($"Guest flow rate {rate}x applied to izakaya configuration.");
        InGameConsole.ShowPassiveFromAnyThread(TextId.CheatFlowRateActive.Get(rateText));
    }

    // MetaMiku 注:
    //     下面分别是 IzakayaConfigure 中 菜单/酒水/厨具 注册与注销 的 hook
    //     但是其中对于 厨具，厨具无论是注册还是注销，都会触发 RegisterToCookers，而只有在注销时才会触发 LogOffFromCookers

    [HarmonyPatch(nameof(IzakayaConfigure.RegisterToDailyRecipes))]
    [HarmonyPrefix]
    public static bool RegisterToDailyRecipes_Prefix(int id)
    {
        Log.LogInfo($"RegisterToDailyRecipes: {id}");

        if (GameSession.HasPeers && !PlayerManager.RecipeAvailable(id))
        {
            Log.LogWarning($"Peer does not have recipe {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerRecipeNotAvailable.Get(id));
            return SkipOriginal;
        }

        PrepSceneManager.localPrepTable.RecipeAdditions[id] = MetaMystia.Multiplayer.RoomClock.SynchronizedNow;
        UpdatePrepAction.Send(PrepSceneManager.localPrepTable);
        return RunOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.RegisterToDailyBeverages))]
    [HarmonyPrefix]
    public static bool RegisterToDailyBeverages_Prefix(int id)
    {
        Log.LogInfo($"RegisterToDailyBeverages: {id}");
        if (GameSession.HasPeers && !PlayerManager.BeverageAvailable(id))
        {
            Log.LogWarning($"Peer does not have beverage {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerBeverageNotAvailable.Get(id));
            return SkipOriginal;
        }

        PrepSceneManager.localPrepTable.BeverageAdditions[id] = MetaMystia.Multiplayer.RoomClock.SynchronizedNow;
        UpdatePrepAction.Send(PrepSceneManager.localPrepTable);
        return RunOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.RegisterToCookers))]
    [HarmonyPrefix]
    public static bool RegisterToCookers_Prefix(int id, int index, bool checkPlayerHaveCooker)
    {
        var slots = PrepSceneManager.GetLocalCookerSlots();
        if (index < 0 || index >= slots.Length)
        {
            Log.LogWarning($"RegisterToCookers out of range: id={id}, index={index}, checkPlayerHaveCooker={checkPlayerHaveCooker}");
            return SkipOriginal;
        }

        if (id != -1 && GameSession.HasPeers && !PlayerManager.CookerAvailable(id))
        {
            Log.LogWarning($"Peer does not have cooker {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerCookerNotAvailable.Get(id));
            return SkipOriginal;
        }

        long timestamp = MetaMystia.Multiplayer.RoomClock.SynchronizedNow;
        slots[index].Id = id;
        slots[index].Timestamp = timestamp;

        Log.LogInfo($"RegisterToCookers: id={id}, index={index}, ts={timestamp}, checkPlayerHaveCooker={checkPlayerHaveCooker}");

        UpdatePrepAction.Send(PrepSceneManager.localPrepTable);
        return RunOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.LogoffFromDailyRecipes))]
    [HarmonyPrefix]
    public static void LogoffFromDailyRecipes_Prefix(int id)
    {
        Log.LogInfo($"LogoffFromDailyRecipes: {id}");
        PrepSceneManager.localPrepTable.RecipeDeletions[id] = MetaMystia.Multiplayer.RoomClock.SynchronizedNow;
        UpdatePrepAction.Send(PrepSceneManager.localPrepTable);
    }

    [HarmonyPatch(nameof(IzakayaConfigure.LogoffFromDailyBeverages))]
    [HarmonyPrefix]
    public static void LogoffFromDailyBeverages_Prefix(int id)
    {
        Log.LogInfo($"LogoffFromDailyBeverages: {id}");
        PrepSceneManager.localPrepTable.BeverageDeletions[id] = MetaMystia.Multiplayer.RoomClock.SynchronizedNow;
        UpdatePrepAction.Send(PrepSceneManager.localPrepTable);
    }

    [HarmonyPatch(nameof(IzakayaConfigure.LogOffFromCookers))]
    [HarmonyPrefix]
    public static void LogOffFromCookers_Prefix(int index)
    {
        Log.LogInfo($"LogOffFromCookers: {index}");
    }


    private static bool _skipPatchStoreFood = false;
    public static void StoreFood_Original(Sellable sellable, int messageSender = -1)
    {
        _skipPatchStoreFood = true;
        IzakayaConfigure.Instance.StoreFood(sellable, messageSender);
        _skipPatchStoreFood = false;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.StoreFood))]
    [HarmonyPrefix]
    public static void StoreFood_Prefix(Sellable sellable)
    {
        Log.LogInfo($"StoreFood: {sellable.Text.Name}");
        if (_skipPatchStoreFood) return;
        if (!GameSession.HasPeers) return;

        var food = SellableFood.FromSellable(sellable);
        StoreFoodAction.Send(food);
    }

}

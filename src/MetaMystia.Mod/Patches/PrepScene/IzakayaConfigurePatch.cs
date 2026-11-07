using System.Globalization;

using HarmonyLib;
using UnityEngine;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
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

    // 菜谱、酒水和厨具逐项同步；厨具注销也会经过 RegisterToCookers。

    [HarmonyPatch(nameof(IzakayaConfigure.RegisterToDailyRecipes))]
    [HarmonyPrefix]
    public static bool RegisterToDailyRecipes_Prefix(int id)
    {
        if (GameSession.HasRoomPeers && !PlayerManager.RecipeAvailable(id))
        {
            Log.LogWarning($"Peer does not have recipe {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerRecipeNotAvailable.Get(id));
            return SkipOriginal;
        }

        new UpdatePrepMessage { AddedRecipes = [id] }.Submit();
        return RunOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.RegisterToDailyBeverages))]
    [HarmonyPrefix]
    public static bool RegisterToDailyBeverages_Prefix(int id)
    {
        if (GameSession.HasRoomPeers && !PlayerManager.BeverageAvailable(id))
        {
            Log.LogWarning($"Peer does not have beverage {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerBeverageNotAvailable.Get(id));
            return SkipOriginal;
        }

        new UpdatePrepMessage { AddedBeverages = [id] }.Submit();
        return RunOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.RegisterToCookers))]
    [HarmonyPrefix]
    public static bool RegisterToCookers_Prefix(int id, int index, bool checkPlayerHaveCooker)
    {
        if (index < 0 || index >= IzakayaConfigure.Instance.CookerConfigure.Length)
        {
            Log.LogWarning($"RegisterToCookers out of range: id={id}, index={index}, checkPlayerHaveCooker={checkPlayerHaveCooker}");
            return SkipOriginal;
        }

        if (id != -1 && GameSession.HasRoomPeers && !PlayerManager.CookerAvailable(id))
        {
            Log.LogWarning($"Peer does not have cooker {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerCookerNotAvailable.Get(id));
            return SkipOriginal;
        }

        new UpdatePrepMessage { ChangedCookers = new() { [index] = id } }.Submit();
        return RunOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigure.LogoffFromDailyRecipes))]
    [HarmonyPrefix]
    public static void LogoffFromDailyRecipes_Prefix(int id) =>
        new UpdatePrepMessage { RemovedRecipes = [id] }.Submit();

    [HarmonyPatch(nameof(IzakayaConfigure.LogoffFromDailyBeverages))]
    [HarmonyPrefix]
    public static void LogoffFromDailyBeverages_Prefix(int id) =>
        new UpdatePrepMessage { RemovedBeverages = [id] }.Submit();

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
        if (!GameSession.HasRoomPeers) return;

        var food = SellableFood.FromSellable(sellable);
        StoreFoodMessage.Send(food);
    }

}

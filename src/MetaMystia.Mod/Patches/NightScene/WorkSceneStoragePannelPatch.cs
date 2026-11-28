using HarmonyLib;

using GameData.Core.Collections;
using NightScene.UI.CookingUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneStoragePannel))]
public class WorkSceneStoragePannelPatch
{
    public static WorkSceneStoragePannel instanceRef = null;

    [HarmonyPatch(nameof(WorkSceneStoragePannel.OnPanelOpen))]
    [HarmonyPostfix]
    public static void OnPanelOpen_Postfix(WorkSceneStoragePannel __instance)
    {
        instanceRef = __instance;
    }

    [HarmonyPatch(nameof(WorkSceneStoragePannel.OnPanelClose))]
    [HarmonyPrefix]
    public static void OnPanelClose_Prefix()
    {
        instanceRef = null;
    }


    [HarmonyPatch(nameof(WorkSceneStoragePannel.Extract))]
    [HarmonyPrefix]
    public static bool OnExtract_Prefix(Sellable toExtract)
    {
        Log.Debug($"Extract: {toExtract?.id}, {toExtract?.Text?.Name}");
        // 料理对决的旁观者可以查看，不能取出。
        if (VersusMatch.IsSpectating) return SkipOriginal;
        if (toExtract.type == Sellable.SellableType.Beverage)
        {
            if (GameSession.HasRoomPeers && !PlayerManager.BeverageAvailable(toExtract.id))
            {
                Log.Warning($"Peer does not have beverage {toExtract.id}, cannot extract.");
                InGameConsole.ShowPassive(TextId.DLCPeerBeverageNotAvailable.Get(toExtract.id));
                return SkipOriginal;
            }
        }
        else if (toExtract.type == Sellable.SellableType.Food)
        {
            if (GameSession.HasRoomPeers && !PlayerManager.FoodAvailable(toExtract.id))
            {
                Log.Warning($"Peer does not have recipe {toExtract.id}, cannot extract.");
                InGameConsole.ShowPassive(TextId.DLCPeerFoodNotAvailable.Get(toExtract.id));
                return SkipOriginal;
            }
            SellableFood food = SellableFood.FromSellable(toExtract);
            ExtractFoodMessage.Send(food);
        }
        return RunOriginal;
    }
}

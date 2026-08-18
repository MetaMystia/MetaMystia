using HarmonyLib;

using GameData.Core.Collections;
using NightScene.UI.CookingUtility;

using MetaMystia.Network;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneStoragePannel))]
[AutoLog]
public partial class WorkSceneStoragePannelPatch
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
        Log.InfoCaller($"{toExtract?.id}, {toExtract?.Text?.Name}");
        if (toExtract.type == Sellable.SellableType.Beverage)
        {
            if (MpManager.IsConnected && !PlayerManager.BeverageAvailable(toExtract.id))
            {
                Log.LogWarning($"Peer does not have beverage {toExtract.id}, cannot extract.");
                InGameConsole.ShowPassive(TextId.DLCPeerBeverageNotAvailable.Get(toExtract.id));
                return SkipOriginal;
            }
        }
        else if (toExtract.type == Sellable.SellableType.Food)
        {
            if (MpManager.IsConnected && !PlayerManager.FoodAvailable(toExtract.id))
            {
                Log.LogWarning($"Peer does not have recipe {toExtract.id}, cannot extract.");
                InGameConsole.ShowPassive(TextId.DLCPeerFoodNotAvailable.Get(toExtract.id));
                return SkipOriginal;
            }
            SellableFood food = SellableFood.FromSellable(toExtract);
            ExtractFoodAction.Send(food);
        }
        return RunOriginal;
    }
}

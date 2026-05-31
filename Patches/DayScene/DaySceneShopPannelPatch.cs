using HarmonyLib;

using Common.UI;
using DayScene.UI;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.UI.DaySceneShopPannel))]
[AutoLog]
public static partial class DaySceneShopPannelPatch
{
    [HarmonyPatch(nameof(DaySceneShopPannel.OnPanelOpen))]
    [HarmonyPostfix]
    public static void OnPanelOpen_Postfix(DaySceneShopPannel __instance)
    {
        var products = __instance.allShelfProductList;
        if (products == null || products.Count == 0)
        {
            Log.Info("Empty shelf detected after OnPanelOpen, removing orphaned custom spacing");
            ReceivedObjectDisplayerController.TryRemoveCustomSpacing<DaySceneShopPannel>();
        }
    }
}

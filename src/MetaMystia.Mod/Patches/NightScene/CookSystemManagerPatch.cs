using HarmonyLib;
using UnityEngine;

using NightScene.CookingUtility;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.CookingUtility.CookSystemManager))]
public static class CookSystemManagerPatch
{
    [HarmonyPatch(nameof(CookSystemManager.CallCooker))]
    [HarmonyPrefix]
    public static bool CallCooker_Prefix(CookSystemManager __instance, Vector3Int cookerPosition)
    {
        if (BusinessStart.IsWaitingForStart)
        {
            // 在打开选材面板或改变厨具状态前阻止交互。
            InGameConsole.ShowPassive(TextId.BusinessStartWaiting.Get());
            return SkipOriginal;
        }

        var cooker = __instance.GetCooker(cookerPosition);
        if (cooker.Phase != CookController.CookPhase.Idle && cooker.Result == null)
        {
            Log.Warning($"Reset cooker with missing result: GridIndex={cooker.GridIndex}, Phase={cooker.Phase}");
            if (cooker.onCookingCoroutine != null)
            {
                cooker.StopCoroutine(cooker.onCookingCoroutine);
                cooker.onCookingCoroutine = null;
            }
            CookControllerPatch.Extract_ReversePatch(cooker, null);
        }
        // 料理对决中他队开火或存放的成品不能取出，也不能在其上交换存放（原版调用方先取出再放入托盘或交换）。
        if (cooker.Result != null && !VersusMatch.CanExtract(cooker.GridIndex)) return SkipOriginal;
        return RunOriginal;
    }
}

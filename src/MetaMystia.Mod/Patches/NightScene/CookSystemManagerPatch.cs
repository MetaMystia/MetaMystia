using HarmonyLib;
using UnityEngine;

using NightScene.CookingUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.CookingUtility.CookSystemManager))]
[AutoLog]
public static partial class CookSystemManagerPatch
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
        return RunOriginal;
    }

    /// <summary>QTE 未满分的符卡惩罚由主机触发后下发。</summary>
    /// <remarks>原方法开头有短跳转，见 il2cppinterop-defects 的 BeverageOut 一节，故在此触发回调，不执行原方法。</remarks>
    [HarmonyPatch(nameof(CookSystemManager.CallQTENotFullScore))]
    [HarmonyPrefix]
    [IcfFolded("与 ListViewController.RaiseItemsRemoved 折叠，按 __instance 的类型区分")]
    public static bool CallQTENotFullScore_Prefix(CookSystemManager __instance, CookController cookController)
    {
        if (__instance.TryCast<CookSystemManager>() == null) return RunOriginal;
        if (GameSession.HasRoomPeers) QTENotFullScoreMessage.Send(cookController.GridIndex);
        if (!GameSession.HasRoomPeers || GameSession.IsRoomHost) __instance.OnQTENotFullScoreCallback?.Invoke(cookController);
        return SkipOriginal;
    }
}

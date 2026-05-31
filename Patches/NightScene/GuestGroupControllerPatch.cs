using HarmonyLib;

using NightScene.GuestManagementUtility;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.GuestGroupController))]
[TracePatch(nameof(GuestGroupController.MoveToQueue))]
[TracePatch(nameof(GuestGroupController.MoveToDesk))]
[TracePatch(nameof(GuestGroupController.GenerateOrder))]
[TracePatch(nameof(GuestGroupController.RemoveFromQueue))]
[TracePatch(nameof(GuestGroupController.MoveToSpawn))]
[TracePatch(nameof(GuestGroupController.FlyToSpawn))]
[TracePatch(nameof(GuestGroupController.RefreshCurrentFundAndOrder))]
[TracePatch(nameof(GuestGroupController.PushToOrder))]
[TracePatch(nameof(GuestGroupController.PeekOrders))]
[TracePatch(nameof(GuestGroupController.PostGenerateOrder))]
[TracePatch(nameof(GuestGroupController.Evaluate))]
[TracePatch(nameof(GuestGroupController.EvaluateUnderSparrowTune))]
[TracePatch(nameof(GuestGroupController.TryOverrideEvaluateByBuff))]
[TracePatch(nameof(GuestGroupController.TryReleaseAllServedFood))]
[TracePatch(nameof(GuestGroupController.UpdateQueuedCharacters))]
[TracePatch(nameof(GuestGroupController.MoveToFirstQueue))]
[TracePatch(nameof(GuestGroupController.MoveToTargetPosition))]
[AutoLog]
public partial class GuestGroupControllerPatch
{
    /// <summary>
    /// RefreshCurrentFundAndOrder 在 _TrySendToSeat_b__0 (OnArrive 回调) 中被调用，
    /// 此时角色刚到达桌位，随后进入 10s/speed 的首单延时。
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(GuestGroupController.RefreshCurrentFundAndOrder))]
    [HarmonyPrefix]
    public static void RefreshCurrentFundAndOrder_Prefix(GuestGroupController __instance)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            GuestFSM.OnRefreshCurrentFundAndOrder(__instance);
        }
    }

    /// <summary>
    /// 主机捕捉 MoveToDesk 的目标桌号并同步。顾客组可能刚生成即可入座，也可能因座满先入队，后被送出队伍入座。
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="deskCode"></param>
    /// <param name="onMovementFinishCallback"></param>
    [HarmonyPatch(nameof(GuestGroupController.MoveToDesk))]
    [HarmonyPrefix]
    public static void MoveToDesk_Prefix(GuestGroupController __instance, int deskCode, ref Il2CppSystem.Action onMovementFinishCallback)
    {
        if (GuestsManagerPatch.IsReimuProtectionGuest(__instance)) return;
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            GuestFSM.OnMoveToDesk(__instance, deskCode);
        }
    }

    /// <summary>
    /// 因座满，主机刚生成的顾客组需要先入队时，主机同步入队事件
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(GuestGroupController.MoveToQueue))]
    [HarmonyPostfix]
    public static void MoveToQueue_Postfix(GuestGroupController __instance)
    {
        // 注：有且只有在 Spell_Orin 的负面符卡中会有 tryToJumpQueue = true
        // TODO(Spell)
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            GuestFSM.OnMoveToQueue(__instance);
        }
    }

    /// <summary>
    /// 主机 hook 用于捕获主机顾客的评价，其后几乎不存在更多改判情况，因此以此判定结果为同步基准。
    /// 客机 hook 用于选择性覆写评价结果。
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="__result"></param>
    [HarmonyPatch(nameof(GuestGroupController.TryOverrideEvaluateByBuff))]
    [HarmonyPostfix]
    public static void TryOverrideEvaluateByBuff_Postfix(GuestGroupController __instance, ref int __result)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;

        var fsm = GuestsMap.GetGuestFsm(__instance);
        if (MpManager.IsConnectedHost)
        {
            var evalResult = GuestsManager.Instance.EvaluationTrans(__result);
            GuestFSM.OnEvaluateOrder(__instance, evalResult);
            return;
        }
        if (MpManager.IsConnectedClient)
        {
            if (fsm.OverrideEvalResult == GuestGroupController.EvaluationResult.Null)
                return;
            __result = (int)fsm.OverrideEvalResult;
        }
    }
}

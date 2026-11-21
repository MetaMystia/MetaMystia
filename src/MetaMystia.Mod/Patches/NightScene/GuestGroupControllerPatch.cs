using HarmonyLib;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;

using static MetaMystia.Patch.HarmonyPrefixFlow;

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
    [HarmonyPatch(nameof(GuestGroupController.Evaluate))]
    [HarmonyPrefix]
    public static bool Evaluate_Prefix(GuestGroupController __instance, ref int __result) =>
        YuyukoGuestSync.OverrideEvaluation(__instance, ref __result) ? SkipOriginal : RunOriginal;

    /// <summary>主机到座处理（TrySendToSeat 的到座回调）开头调用，记录已到座。客机由重放的到座处理记录。</summary>
    [HarmonyPatch(nameof(GuestGroupController.RefreshCurrentFundAndOrder))]
    [HarmonyPrefix]
    public static void RefreshCurrentFundAndOrder_Prefix(GuestGroupController __instance)
    {
        if (GuestRegistry.Mode(__instance) == GuestMode.Host && GuestRegistry.Of(__instance) is { State: GuestPhase.Seating } guest)
            guest.State = GuestPhase.Seated;
    }

    /// <summary>主机入队收口。</summary>
    [HarmonyPatch(nameof(GuestGroupController.MoveToQueue))]
    [HarmonyPostfix]
    public static void MoveToQueue_Postfix(GuestGroupController __instance, bool tryToJumpQueue)
    {
        if (GuestRegistry.Mode(__instance) == GuestMode.Host) MoveToQueueMessage.Detect(__instance, tryToJumpQueue);
    }

    /// <summary>主机未入座离开收口：生成后无处可去、排队耐心耗尽、打烊清队、符卡驱逐。</summary>
    [HarmonyPatch(nameof(GuestGroupController.MoveToSpawn))]
    [HarmonyPrefix]
    public static void MoveToSpawn_Prefix(GuestGroupController __instance)
    {
        if (GuestRegistry.Mode(__instance) == GuestMode.Host) GuestLeaveMessage.DetectUnseated(__instance, false, false);
    }

    [HarmonyPatch(nameof(GuestGroupController.FlyToSpawn))]
    [HarmonyPrefix]
    public static void FlyToSpawn_Prefix(GuestGroupController __instance, bool shouldFade)
    {
        if (GuestRegistry.Mode(__instance) == GuestMode.Host) GuestLeaveMessage.DetectUnseated(__instance, true, shouldFade);
    }

    /// <summary>
    /// 主机记下评价已开始，最终结果在评价台词显示时广播。客机重放评价时换成主机结果。
    /// </summary>
    [HarmonyPatch(nameof(GuestGroupController.TryOverrideEvaluateByBuff))]
    [HarmonyPostfix]
    public static void TryOverrideEvaluateByBuff_Postfix(GuestGroupController __instance, ref int __result)
    {
        switch (GuestRegistry.Mode(__instance))
        {
            case GuestMode.Host:
                EvaluateOrderMessage.MarkPending(__instance);
                break;
            case GuestMode.Client when GuestRegistry.Of(__instance).FinalEval is var final && final != GuestGroupController.EvaluationResult.Null:
                __result = (int)final;
                break;
        }
    }
}

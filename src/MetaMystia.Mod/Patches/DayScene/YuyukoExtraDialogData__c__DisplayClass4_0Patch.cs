using HarmonyLib;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.DaySceneUtility.Collections.YuyukoExtraDialogData.__c__DisplayClass4_0))]
[AutoLog]
public partial class YuyukoExtraDialogData__c__DisplayClass4_0Patch
{
    // Yuyuko_Challenge 的确认回调：先 ScheduleEventExtern，再 StartChallengeSession。
    // 保留整个回调，避免改选营业后残留尚未进入的挑战事件。
    [HarmonyPatch(nameof(GameData.Core.Collections.DaySceneUtility.Collections.YuyukoExtraDialogData.__c__DisplayClass4_0._Yuyuko_Challenge_b__2))]
    [HarmonyPrefix]
    public static bool _Yuyuko_Challenge_b__2_Prefix(GameData.Core.Collections.DaySceneUtility.Collections.YuyukoExtraDialogData.__c__DisplayClass4_0 __instance, bool confirm)
    {
        if (!confirm || !MpManager.IsConnected || DayDestinationManager.ReplayingChallenge) return RunOriginal;
        DayDestinationManager.Submit(DayDestination.FinalTrialAgain, () => __instance._Yuyuko_Challenge_b__2(true));
        return SkipOriginal;
    }
}

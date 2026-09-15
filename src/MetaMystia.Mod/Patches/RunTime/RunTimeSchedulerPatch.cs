using HarmonyLib;

using GameData.Profile;
using GameData.RunTime.Common;
using NightScene;

using static MetaMystia.Patch.HarmonyPrefixFlow;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeScheduler))]
[AutoLog]
[TracePatch("Method_Internal_Static_Void_Action_PDM_0", DisplayName = "ReimuProtection")]
public partial class RunTimeSchedulerPatch
{
    private static bool firstTrialGuest;

    public static void ResetFirstTrialGuest() => firstTrialGuest = false;

    public static void EnterFirstTrialAsGuest()
    {
        firstTrialGuest = true;
        // 已核对游戏配置：首次 P1/P2/P3 可重复，无奖励；成功节点会推进主线，
        // 因此跟随首次的重修玩家使用首次战斗事件、重修收尾事件。
        RunTimeScheduler.ScheduleEventExtern("Main_5_BambooForest_023_Challange_P1");
        RunTimeScheduler.ProcessReward(new SchedulerNode.Reward
        {
            rewardType = SchedulerNode.Reward.RewardType.MoveToChallenge,
            challengeType = NightSceneDirector.ChallengeType.Story_Yuyuko,
            should = false,
        });
    }

    [HarmonyPatch(nameof(RunTimeScheduler.ScheduleEvent))]
    [HarmonyPrefix]
    public static bool ScheduleEvent_Prefix(ref string eventLabel)
    {
        if (!firstTrialGuest) return RunOriginal;
        switch (eventLabel)
        {
            case "Main_5_BambooForest_023_Challenge_Succeed":
                eventLabel = "Challenge_Finale_P3_Succeed";
                break;
            case "Main_5_BambooForest_023_Challange_Failed":
                eventLabel = "Challenge_Finale_P3_Failed";
                break;
            case "Challenge_Finale_Succeed_Back_First":
                return SkipOriginal;
        }
        return RunOriginal;
    }

    // 首次挑战由 MoveToChallenge 奖励进入。重放奖励，保持后续挑战调用在原生侧；
    // 不 Hook StartChallengeSession，其 Nullable<int> 参数在 trampoline 中会封送失败。
    [HarmonyPatch(nameof(RunTimeScheduler.ProcessReward))]
    [HarmonyPrefix]
    public static bool ProcessReward_Prefix(SchedulerNode.Reward reward)
    {
        if (!GameSession.IsInRoom || DayDestinationManager.ReplayingChallenge
            || reward.rewardType != SchedulerNode.Reward.RewardType.MoveToChallenge) return RunOriginal;
        if (reward.challengeType is not (NightSceneDirector.ChallengeType.Story_Yuyuko
            or NightSceneDirector.ChallengeType.Challenge_Yuyuko)) return RunOriginal;
        var destination = reward.challengeType == NightSceneDirector.ChallengeType.Story_Yuyuko
            ? DayDestination.FinalTrial : DayDestination.FinalTrialAgain;
        DayDestinationManager.Submit(destination, () => RunTimeScheduler.ProcessReward(reward));
        return SkipOriginal;
    }

    // InvokeDayOverEventsAsync 依次等待这两个回调；首次挑战等待期间不能继续选店。
    [HarmonyPatch(nameof(RunTimeScheduler.OnDayEnd))]
    [HarmonyPrefix]
    public static void OnDayEnd_Prefix(ref Il2CppSystem.Action onFinish) => GuardDayEnd(ref onFinish);

    [HarmonyPatch(nameof(RunTimeScheduler.OnAfterDayEnd))]
    [HarmonyPrefix]
    public static void OnAfterDayEnd_Prefix(ref Il2CppSystem.Action onFinish) => GuardDayEnd(ref onFinish);

    private static void GuardDayEnd(ref Il2CppSystem.Action onFinish)
    {
        if (!GameSession.IsInRoom) return;
        var continuation = onFinish;
        onFinish = (System.Action)(() => DayDestinationManager.FinishDayEnd(continuation));
    }

    public static readonly PatchBypassToken DuringReimuProtection = new();
    public static bool IsDuringReimuProtection => DuringReimuProtection.Pending > 0;

    // <AddReimuPositiveSpellToWorkScene>g__ReimuProtection|160_0(Action onFinish)
    // VA = 0x18064F250 in Release 4.3.0c
    [HarmonyPatch(nameof(RunTimeScheduler.Method_Internal_Static_Void_Action_PDM_0))]
    [HarmonyPrefix]
    public static void ReimuProtection_Prefix()
    {
        Log.Info("ReimuProtection prefix called.");
        DuringReimuProtection.Grant();
    }

    [HarmonyPatch(nameof(RunTimeScheduler.Method_Internal_Static_Void_Action_PDM_0))]
    [HarmonyPostfix]
    public static void ReimuProtection_Postfix()
    {
        Log.Info("ReimuProtection postfix called.");
        DuringReimuProtection.Reset();
    }
}

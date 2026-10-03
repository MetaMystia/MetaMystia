using System;

using GameData.Profile;
using GameData.RunTime.Common;
using NightScene;

using Mystia.Listeners;

using MetaMystia.Multiplayer;

namespace MetaMystia.Listeners;

/// <summary>
/// 日程监听，取代原 <c>Patches/Compat/RunTimeSchedulerPatch</c> 与 <c>RunTimeSchedulerGapsPatch</c>：
/// 事件改写（跟随首次的「重修」玩家）、奖励拦截与重放、灵梦保护窗口、白天结束与结束后的回调包装。
/// </summary>
[AutoLog]
public sealed partial class ScheduleSync : IScheduleListener
{
    /// <summary>本轮白天是否以「重修玩家跟随首次挑战」的方式进入（原 <c>RunTimeSchedulerPatch.firstTrialGuest</c>）。</summary>
    private static bool s_firstTrialGuest;

    /// <summary>灵梦送钱保护窗口的嵌套深度；<see cref="OnReimuProtectionEntered"/> 进入、<see cref="OnReimuProtectionExited"/> 清零。</summary>
    private static int s_reimuProtection;

    /// <summary>是否处于灵梦送钱保护窗口，供顾客同步判定保护来客（原 <c>RunTimeSchedulerGapsPatch.DuringReimuProtection</c>）。</summary>
    public static bool IsDuringReimuProtection => s_reimuProtection > 0;

    public static void ResetFirstTrialGuest() => s_firstTrialGuest = false;

    public static void EnterFirstTrialAsGuest()
    {
        s_firstTrialGuest = true;
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

    /// <summary>原 <c>ScheduleEvent_Prefix</c>：改写事件标签或拦下该次排期。</summary>
    public void OnPreScheduleEvent(ref string eventLabel, ref bool cancelInvocation)
    {
        if (!s_firstTrialGuest)
            return;

        switch (eventLabel)
        {
            case "Main_5_BambooForest_023_Challenge_Succeed":
                eventLabel = "Challenge_Finale_P3_Succeed";
                break;
            case "Main_5_BambooForest_023_Challange_Failed":
                eventLabel = "Challenge_Finale_P3_Failed";
                break;
            case "Challenge_Finale_Succeed_Back_First":
                // 首次挑战由 MoveToChallenge 奖励进入，这条收尾事件不再排期。
                cancelInvocation = true;
                break;
        }
    }

    public void OnPreDayEnd(ref Action onFinished, ref bool cancelInvocation) => GuardDayEnd(ref onFinished);

    public void OnPreAfterDayEnd(ref Action onFinished, ref bool cancelInvocation) => GuardDayEnd(ref onFinished);

    /// <summary>
    /// 首次/重修挑战由 <c>MoveToChallenge</c> 奖励进入。原补丁在 <c>ProcessReward</c> 前缀里拦下该奖励，
    /// 先在 <see cref="DayDestinationManager"/> 上登记目的地，再在确认后重放同一个奖励以保持后续挑战调用
    /// 在原生侧；不 Hook <c>StartChallengeSession</c>，其 Nullable 参数在 trampoline 中会封送失败。
    /// </summary>
    public void OnPreRewardProcessed(ref SchedulerNode.Reward reward, ref bool cancelInvocation)
    {
        if (!GameSession.IsInRoom || DayDestinationManager.ReplayingChallenge) return;
        if (reward.rewardType != SchedulerNode.Reward.RewardType.MoveToChallenge) return;
        if (reward.challengeType is not (NightSceneDirector.ChallengeType.Story_Yuyuko
            or NightSceneDirector.ChallengeType.Challenge_Yuyuko)) return;

        var destination = reward.challengeType == NightSceneDirector.ChallengeType.Story_Yuyuko
            ? DayDestination.FinalTrial : DayDestination.FinalTrialAgain;
        var captured = reward;
        cancelInvocation = true;
        DayDestinationManager.Submit(destination, () => RunTimeScheduler.ProcessReward(captured));
    }

    /// <summary>灵梦送钱保护窗口打开：调度器即将把灵梦正面符卡加进营业场景。</summary>
    public void OnReimuProtectionEntered() => s_reimuProtection++;

    /// <summary>灵梦送钱保护窗口关闭；与 <see cref="OnReimuProtectionEntered"/> 配对。</summary>
    public void OnReimuProtectionExited() => s_reimuProtection = 0;

    /// <summary>
    /// 原 <c>GuardDayEnd</c>：<c>InvokeDayOverEventsAsync</c> 依次等待这两个回调；
    /// 首次挑战等待期间不能继续选店，因此把回调包一层，由 <see cref="DayDestinationManager"/> 决定是否继续。
    /// </summary>
    private static void GuardDayEnd(ref Action onFinished)
    {
        if (!GameSession.IsInRoom)
            return;

        var continuation = onFinished;
        onFinished = () => DayDestinationManager.FinishDayEnd(continuation);
    }
}

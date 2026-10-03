using System;

using GameData.Profile;
using GameData.RunTime.Common;
using NightScene;

using Mystia.Listeners;

using MetaMystia.Multiplayer;

namespace MetaMystia.Listeners;

/// <summary>
/// 日程监听，取代原 <c>Patches/Compat/RunTimeSchedulerPatch</c> 中可由中间件表达的部分：
/// 事件改写（跟随首次的「重修」玩家）、白天结束与结束后的回调包装。
/// <para>灵梦保护窗口没有 enter/exit 通知对，奖励拦截拿不到被处理的奖励数据，
/// 二者仍以 Harmony 形式保留在 <c>Patches/Compat/RunTimeSchedulerGapsPatch</c>，见该文件与缺口清单。</para>
/// </summary>
[AutoLog]
public sealed partial class ScheduleSync : IScheduleListener
{
    /// <summary>本轮白天是否以「重修玩家跟随首次挑战」的方式进入（原 <c>RunTimeSchedulerPatch.firstTrialGuest</c>）。</summary>
    private static bool s_firstTrialGuest;

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

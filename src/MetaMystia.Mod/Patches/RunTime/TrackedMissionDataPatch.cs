using HarmonyLib;

using GameData.Core.Collections;
using GameData.Profile;
using GameData.RunTime.Common;

using static GameData.Profile.SchedulerNodeCollection.MissionNode.FinishCondition;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeScheduler.TrackedMissionData))]
[AutoLog]
public partial class TrackedMissionDataPatch
{
    /// <summary>
    /// 部分 ResourceEx MissionNode 要求使用带有 <b>相对日期</b> 的 <see cref="ConditionType.BillRepayment"/> 类型，而原游戏使用绝对日期与 day 字面量进行比较，导致条件错误。此处 Patch 用以修正。
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(RunTimeScheduler.TrackedMissionData.UpdateFinishStates))]
    [HarmonyPostfix]
    public static void UpdateFinishStates_Postfix(RunTimeScheduler.TrackedMissionData __instance)
    {
        if (!__instance.missionLabel.StartsWith("_")) return;

        var mission = DataBaseScheduler.RefMission(__instance.missionLabel);
        if (mission.missionTimeLimit.time.dayType != SchedulerNode.Day.DayType.Relative) return;

        var triggerTime = RunTimeScheduler.FindMissionTriggerTime(__instance);
        var today = RunTimePlayerData.GetDay();
        var justToday = triggerTime == today.CorrectedDay;

        for (int i = 0; i < mission.finishCondition.Length; i++)
        {
            var condition = mission.finishCondition[i];
            if (condition.conditionType != ConditionType.BillRepayment) // 不得检查 condition == null
            {
                continue;
            }
            if (RunTimeScheduler.CurrentGamePhase != RunTimeScheduler.GamePhase.WorkEnd)
            {
                continue;
            }

            __instance.conditionFinishStates[i] = RunTimePlayerData.GetFund() >= condition.amount && justToday;
        }
    }
}

using HarmonyLib;

using GameData.Core.Collections;
using GameData.Profile;
using GameData.RunTime.Common;

using static GameData.Profile.SchedulerNodeCollection.MissionNode.FinishCondition;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeScheduler.TrackedMissionData))]
public class TrackedMissionDataPatch
{
    /// <summary>
    /// 原生 <see cref="ConditionType.BillRepayment"/> 拿今天与配置的 day 字面量比较：相对日期的 ResourceEx 任务永远不成立，
    /// 读档恢复时顺延过截止日的绝对日期任务也不成立。这里改用该任务实际所在的截止日（追踪表的 key）。
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(RunTimeScheduler.TrackedMissionData.UpdateFinishStates))]
    [HarmonyPostfix]
    public static void UpdateFinishStates_Postfix(RunTimeScheduler.TrackedMissionData __instance)
    {
        if (!__instance.missionLabel.StartsWith("_")) return;

        var mission = DataBaseScheduler.RefMission(__instance.missionLabel);
        if (!mission.isTimedMission) return;

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

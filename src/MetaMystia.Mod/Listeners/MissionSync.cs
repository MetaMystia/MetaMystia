using GameData.Core.Collections;
using GameData.Profile;
using GameData.RunTime.Common;

using Mystia.Listeners;

using static GameData.Profile.SchedulerNodeCollection.MissionNode.FinishCondition;

namespace MetaMystia.Listeners;

/// <summary>
/// 任务完成条件修正（原 <c>TrackedMissionDataPatch.UpdateFinishStates_Postfix</c>）。
/// 部分 ResourceEx MissionNode 要求使用带有<b>相对日期</b>的 <see cref="ConditionType.BillRepayment"/>，
/// 而原游戏用绝对日期与 day 字面量比较；条件重算后按「今日即触发日」重写该条件。
/// </summary>
[AutoLog]
public sealed partial class MissionSync : IMissionListener
{
    public void OnMissionFinishStatesUpdated(RunTimeScheduler.TrackedMissionData mission)
    {
        if (!mission.missionLabel.StartsWith("_")) return;

        var definition = DataBaseScheduler.RefMission(mission.missionLabel);
        if (definition.missionTimeLimit.time.dayType != SchedulerNode.Day.DayType.Relative) return;

        var triggerTime = RunTimeScheduler.FindMissionTriggerTime(mission);
        var justToday = triggerTime == RunTimePlayerData.GetDay().CorrectedDay;

        for (var i = 0; i < definition.finishCondition.Length; i++)
        {
            var condition = definition.finishCondition[i];
            if (condition.conditionType != ConditionType.BillRepayment) // 不得检查 condition == null
            {
                continue;
            }
            if (RunTimeScheduler.CurrentGamePhase != RunTimeScheduler.GamePhase.WorkEnd)
            {
                continue;
            }

            mission.conditionFinishStates[i] = RunTimePlayerData.GetFund() >= condition.amount && justToday;
        }
    }
}

using System;
using HarmonyLib;

using GameData.Core.Collections;
using GameData.RunTime.Common;

using static GameData.Profile.SchedulerNode;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(RunTimeScheduler))]
[AutoLog]
public partial class RunTimeSchedulerPatch
{
    /// <summary>
    /// StartMission 后，将限时任务的 missionTimeLimit.time 从 Relative 归一化为 Absolute。
    /// </summary>
    [HarmonyPatch("StartMission")]
    [HarmonyPatch(new Type[] { typeof(string) })]
    [HarmonyPostfix]
    public static void StartMission_Postfix(string missionLabel)
    {
        NormalizeMissionTimeLimit(missionLabel, "[StartMission]");
    }

    /// <summary>
    /// ExecuteTimedMissionCheckpoint 前，确保限时任务已归一化。
    /// 读档后 MissionNode 从配置重建，time.day 回到 Relative，
    /// 但 StartMission 不会重跑。此 Patch 兜底，每次 checkpoint 前修正。
    ///
    /// 注意：不能使用 time.GetAbsoluteDay()，因为它用当前 CorrectedDay 重新计算，
    /// 会导致截止日变成"当前天+偏移"而非"启动天+偏移"。
    /// 必须用 FindMissionTriggerTime 从 trackingMissions key 取原始截止日。
    /// </summary>
    [HarmonyPatch("ExecuteTimedMissionCheckpoint")]
    [HarmonyPrefix]
    public static void ExecuteTimedMissionCheckpoint_Prefix(RunTimeScheduler.TrackedMissionData trackingMissionData)
    {
        if (trackingMissionData == null) return;

        var missionNode = DataBaseScheduler.RefMission(trackingMissionData.missionLabel);
        if (missionNode == null || !missionNode.isTimedMission) return;
        if (missionNode.missionTimeLimit.time.dayType == Day.DayType.Absolute) return;

        // 从 trackingMissions 中获取 StartMission 时算好的截止日 key
        int absoluteDay = RunTimeScheduler.FindMissionTriggerTime(trackingMissionData);

        var trigger = missionNode.missionTimeLimit;
        var time = trigger.time;
        time.dayType = Day.DayType.Absolute;
        time.dayCalcType = Day.CalculateType.Constant;
        time.day = absoluteDay;
        trigger.time = time;
        missionNode.missionTimeLimit = trigger;

        Log.Info($"[TimedMission] '{trackingMissionData.missionLabel}' [Checkpoint]: normalized to Absolute {absoluteDay}");
    }

    private static void NormalizeMissionTimeLimit(string missionLabel, string source)
    {
        var missionNode = DataBaseScheduler.RefMission(missionLabel);
        if (missionNode == null || !missionNode.isTimedMission)
            return;
        if (missionNode.missionTimeLimit.time.dayType == Day.DayType.Absolute)
            return;

        var trigger = missionNode.missionTimeLimit;
        var time = trigger.time;
        int absoluteDay = time.GetAbsoluteDay();
        time.dayType = Day.DayType.Absolute;
        time.dayCalcType = Day.CalculateType.Constant;
        time.day = absoluteDay;
        trigger.time = time;
        missionNode.missionTimeLimit = trigger;

        Log.Info($"[TimedMission] '{missionLabel}' {source}: normalized to Absolute {absoluteDay}");
    }
}

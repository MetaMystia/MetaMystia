using System.Collections.Generic;

using GameData.RunTime.Common;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 存档 Scheduler 数据恢复：清理或重载旧版本 mod 遗留的 UNDEFINED / ResourceEx 未加载数据。
/// </summary>
[AutoLog]
public static partial class SchedulerDataRecovery
{
    internal static void CheckAndReloadSchedulerData()
    {
        // 检查是否有大量的 UNDEFINED 或 ResourceEx 标识的 Scheduler 数据未加载，这可能是由于使用旧版本 mod 时反复触发任务并 Save/Load 导致的
        var notLoadedDLCSchedulerSaveData = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;

        var targetKeys = new List<string>();
        foreach (var key in notLoadedDLCSchedulerSaveData.Keys)
        {
            if (key.StartsWith("UNDEFINED") || key.StartsWith("ResourceEx"))
            {
                targetKeys.Add(key);
            }
        }

        if (targetKeys.Count > 1)
        {
            // 数量超过 1，判定需要进行重置。清除全部 UNDEFINED 和 ResourceEx 相关的未加载数据，并避免误伤 DLC* 的数据
            // 由于没有其他 mod，此时可以认定这些数据均为本 mod 产生
            Log.Warning($"Detected {targetKeys.Count} to be removed from NotLoadedDLCSchedulerSaveData.");
            foreach (var key in targetKeys)
            {
                notLoadedDLCSchedulerSaveData.Remove(key);
                Log.Warning($"Removed notLoadedDLCSchedulerSaveData entry: {key}");
            }
            return;
        }

        if (!notLoadedDLCSchedulerSaveData.ContainsKey("ResourceEx"))
        {
            // 没有 ResourceEx 相关数据，直接返回
            Log.Info("No ResourceEx Scheduler data detected to reload.");
            return;
        }

        // 有且只有一条 ResourceEx 相关数据，进行自动 reload
        Log.Info("No excessive ResourceEx Scheduler data detected. Retaining existing ResourceEx data.");
        var resourceExData = notLoadedDLCSchedulerSaveData["ResourceEx"];

        // reload finishedEvents
        foreach (var finishedEvent in resourceExData.finishedEvents)
        {
            if (!RunTimeScheduler.finishedEvents.Contains(finishedEvent) && EventNodeRegistry.GetAllEventNodeLabels().Contains(finishedEvent))
            {
                RunTimeScheduler.finishedEvents.Add(finishedEvent);
                Log.Info($"Reloaded finishedEvent: {finishedEvent}");
            }
        }

        // reload finishedMissions
        foreach (var finishedMission in resourceExData.finishedMissions)
        {
            if (!RunTimeScheduler.finishedMissions.Contains(finishedMission) && MissionNodeRegistry.GetAllMissionNodeLabels().Contains(finishedMission))
            {
                RunTimeScheduler.finishedMissions.Add(finishedMission);
                Log.Info($"Reloaded finishedMission: {finishedMission}");
            }
        }

        // reload scheduledEvents
        foreach (var scheduledEvent in resourceExData.scheduledEvents)
        {
            if (!RunTimeScheduler.scheduledEvents.ContainsKey(scheduledEvent.Key))
            {
                RunTimeScheduler.scheduledEvents.Add(scheduledEvent.Key, new Il2CppSystem.Collections.Generic.List<string>());
                Log.Info($"Added new scheduledEvents key: {scheduledEvent.Key}");
            }

            var targetList = RunTimeScheduler.scheduledEvents[scheduledEvent.Key];
            foreach (var eventLabel in scheduledEvent.Value)
            {
                if (!targetList.Contains(eventLabel) && EventNodeRegistry.GetAllEventNodeLabels().Contains(eventLabel))
                {
                    targetList.Add(eventLabel);
                    Log.Info($"Reloaded scheduledEvent: {eventLabel} under key: {scheduledEvent.Key}");
                }
            }
        }

        // reload allTrackingMissions
        foreach (var trackingMission in resourceExData.allTrackingMissions)
        {
            if (!RunTimeScheduler.trackingMissions.ContainsKey(trackingMission.Key))
            {
                RunTimeScheduler.trackingMissions.Add(trackingMission.Key, new Il2CppSystem.Collections.Generic.List<RunTimeScheduler.TrackedMissionData>());
                Log.Info($"Added new trackingMissions key: {trackingMission.Key}");
            }

            var targetList = RunTimeScheduler.trackingMissions[trackingMission.Key];
            foreach (var missionData in trackingMission.Value)
            {
                if (!targetList.Contains(missionData) && MissionNodeRegistry.GetAllMissionNodeLabels().Contains(missionData.missionLabel))
                {
                    targetList.Add(missionData);
                    Log.Info($"Reloaded trackingMission: {missionData.missionLabel} under key: {trackingMission.Key}");
                }
            }
        }
        notLoadedDLCSchedulerSaveData.Remove("ResourceEx");
        Log.Info("ResourceEx Scheduler data has been successfully reloaded.");
    }
}

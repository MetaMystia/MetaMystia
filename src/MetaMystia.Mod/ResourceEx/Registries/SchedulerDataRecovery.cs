using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime.InteropTypes.Arrays;

using GameData.RunTime.Common;

using ReplaceContents = Il2CppSystem.Collections.Generic.KeyValuePair<string, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameData.RunTime.Common.RunTimeScheduler.HistoryNewsData.ReplaceContent>>;
using Carrier = GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData;
using IlDict = Il2CppSystem.Collections.Generic;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 资源包的 Scheduler 存档数据。原版存档时按标签映射把它们分进 <c>schedulerPartialDLC["ResourceEx"]</c>（未注册的标签进 <c>UNDEFINED</c>），
/// 读档时这些键不在 ActiveDLCLabel 中，留在 <see cref="RunTimePlayerData.NotLoadedDLCSchedulerSaveData"/>，由这里在首次进入白天时并回运行时。
/// <list type="bullet">
/// <item>日期 key 按原生 DLC 分区的规则加上 <c>今天 - dlcSaveDate</c>：分区在未加载期间经过的天数不计入。-1（不限时事件）和 0（不限时任务）不动；原生连 0 也平移，这里不跟随。</item>
/// <item>未加载资源包的条目（含完成记录）移入 <see cref="PendingKey"/> 分区，以今天为基准保存剩余天数；原版原样写回存档，资源包装回后再恢复。</item>
/// </list>
/// </summary>
public static class SchedulerDataRecovery
{
    /// <summary>不以 ResourceEx、UNDEFINED 开头：旧版模组见到两个以上这类键会清空全部。</summary>
    internal const string PendingKey = "MetaMystia.ResourceEx";

    internal static void CheckAndReloadSchedulerData()
    {
        var notLoaded = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;
        if (notLoaded == null) return;

        var sources = new List<string>();
        foreach (var key in notLoaded.Keys)
        {
            if (IsSource(key)) sources.Add(key);
        }
        if (sources.Count == 0)
        {
            Log.Info("No ResourceEx Scheduler data to reload.");
            return;
        }

        int today = RunTimePlayerData.GetDay().CorrectedDay;
        var restorer = new Restorer(
            EventNodeRegistry.GetAllEventNodeLabels().ToHashSet(),
            MissionNodeRegistry.GetAllMissionNodeLabels().ToHashSet(),
            NewsNodeRegistry.GetAllNewsNodeLabels().ToHashSet());

        // 只剩留存分区且其中没有可恢复的条目时不改动：恢复时按经过的天数平移，留存期间本来就不计时。
        // 读档后的第一次进入白天之后通常就是这种情况，此后每天进入白天都不再写未加载数据。
        if (sources.Count == 1 && sources[0] == PendingKey && !restorer.CanRestoreAny(notLoaded[PendingKey])) return;

        // 新数据先于旧的留存数据：同名事件、任务只保留一份，以先到的为准。
        foreach (var key in sources.OrderBy(k => k == PendingKey ? 2 : k.StartsWith("UNDEFINED") ? 1 : 0).ThenBy(k => k, StringComparer.Ordinal))
        {
            var data = notLoaded[key];
            int diff = data.dlcSaveDate > 0 ? today - data.dlcSaveDate : 0;
            var counts = restorer.Restore(data, diff);
            notLoaded.Remove(key);
            Log.Info($"Reloaded Scheduler data {key} (saved on day {data.dlcSaveDate}, today {today}, shift {diff:+0;-0;0}): {counts}");
        }

        if (restorer.Pending.IsEmpty) return;
        // 值是装箱的原生结构体，经非泛型接口写入，见 docs/reference/il2cppinterop-defects.md。
        notLoaded.Cast<Il2CppSystem.Collections.IDictionary>()[PendingKey] = restorer.Pending.ToCarrier(today);
        Log.Info($"Kept Scheduler data of unloaded resource packs in {PendingKey}: {restorer.Pending.Summary}");
    }

    /// <summary><c>ResourceEx</c>、<c>UNDEFINED</c> 及原生撞名时加的数字后缀，和本模组的留存分区。</summary>
    private static bool IsSource(string key)
        => key == PendingKey || IsNumbered(key, "ResourceEx") || IsNumbered(key, "UNDEFINED");

    private static bool IsNumbered(string key, string prefix)
        => key.StartsWith(prefix, StringComparison.Ordinal) && key.Skip(prefix.Length).All(c => c is >= '0' and <= '9');

    /// <summary>日期 key 平移；-1、0 是不限时分组，结果不得落进这两个值。</summary>
    internal static int Shift(int day, int diff)
    {
        if (day <= 0 || diff == 0) return day;
        return (int)Math.Clamp((long)day + diff, 1, int.MaxValue);
    }

    private sealed class Restorer(HashSet<string> events, HashSet<string> missions, HashSet<string> news)
    {
        public readonly PendingData Pending = new();

        public bool CanRestoreAny(Carrier data)
        {
            foreach (var label in data.finishedEvents ?? new Il2CppStringArray(0)) if (events.Contains(label)) return true;
            foreach (var label in data.finishedMissions ?? new Il2CppStringArray(0)) if (missions.Contains(label)) return true;
            if (data.scheduledEvents != null) foreach (var kv in data.scheduledEvents) foreach (var label in kv.Value) if (events.Contains(label)) return true;
            if (data.scheduledNews != null) foreach (var kv in data.scheduledNews) foreach (var label in kv.Value) if (news.Contains(label)) return true;
            if (data.allTrackingMissions != null) foreach (var kv in data.allTrackingMissions) foreach (var m in kv.Value) if (missions.Contains(m.missionLabel)) return true;
            return false;
        }

        public string Restore(Carrier data, int diff)
        {
            int restored = 0, pending = 0;
            void Count(bool ok) { if (ok) restored++; else pending++; }

            foreach (var label in data.finishedEvents ?? new Il2CppStringArray(0))
            {
                bool ok = events.Contains(label);
                if (ok) { if (!RunTimeScheduler.finishedEvents.Contains(label)) RunTimeScheduler.finishedEvents.Add(label); }
                else Pending.FinishedEvents.Add(label);
                Count(ok);
            }

            foreach (var label in data.finishedMissions ?? new Il2CppStringArray(0))
            {
                bool ok = missions.Contains(label);
                if (ok) { if (!RunTimeScheduler.finishedMissions.Contains(label)) RunTimeScheduler.finishedMissions.Add(label); }
                else Pending.FinishedMissions.Add(label);
                Count(ok);
            }

            // 原生 ScheduleEvent、StartMission 拒绝已排程的同名节点，这里同样按标签只保留一份。
            if (data.scheduledEvents != null)
                foreach (var kv in data.scheduledEvents)
                {
                    int day = Shift(kv.Key, diff);
                    foreach (var label in kv.Value)
                    {
                        bool ok = events.Contains(label);
                        if (ok) { if (!RunTimeScheduler.HaveEventScheduled(label)) At(RunTimeScheduler.scheduledEvents, day).Add(label); }
                        else if (!Contains(Pending.Events, label)) At(Pending.Events, day).Add(label);
                        Count(ok);
                    }
                }

            // 新闻可以重复排程，每条都是一次发布，不去重。
            if (data.scheduledNews != null)
                foreach (var kv in data.scheduledNews)
                {
                    int day = Shift(kv.Key, diff);
                    foreach (var label in kv.Value)
                    {
                        bool ok = news.Contains(label);
                        At(ok ? RunTimeScheduler.scheduledNews : Pending.News, day).Add(label);
                        Count(ok);
                    }
                }

            if (data.scheduledNewsReplaceContents != null)
                foreach (var kv in data.scheduledNewsReplaceContents)
                {
                    int day = Shift(kv.Key, diff);
                    foreach (var item in kv.Value)
                    {
                        bool ok = news.Contains(item.Key);
                        At(ok ? RunTimeScheduler.scheduledNewsReplaceContents : Pending.ReplaceContents, day).Add(item);
                    }
                }

            if (data.allTrackingMissions != null)
                foreach (var kv in data.allTrackingMissions)
                {
                    int day = Shift(kv.Key, diff);
                    foreach (var mission in kv.Value)
                    {
                        var label = mission.missionLabel;
                        bool ok = missions.Contains(label);
                        if (ok) { if (!RunTimeScheduler.HaveMissionStarted(label)) At(RunTimeScheduler.trackingMissions, day).Add(mission); }
                        else if (!Contains(Pending.Missions, label)) At(Pending.Missions, day).Add(mission);
                        Count(ok);
                    }
                }

            return $"restored {restored}, pending {pending}";
        }
    }

    private sealed class PendingData
    {
        public readonly IlDict.Dictionary<int, IlDict.List<string>> Events = new();
        public readonly IlDict.Dictionary<int, IlDict.List<string>> News = new();
        public readonly IlDict.Dictionary<int, IlDict.List<ReplaceContents>> ReplaceContents = new();
        public readonly IlDict.Dictionary<int, IlDict.List<RunTimeScheduler.TrackedMissionData>> Missions = new();
        public readonly SortedSet<string> FinishedEvents = new(StringComparer.Ordinal);
        public readonly SortedSet<string> FinishedMissions = new(StringComparer.Ordinal);

        public bool IsEmpty => Events.Count == 0 && News.Count == 0 && ReplaceContents.Count == 0 && Missions.Count == 0
            && FinishedEvents.Count == 0 && FinishedMissions.Count == 0;

        public string Summary => $"events {Count(Events)}, news {Count(News)}, missions {Count(Missions)}, "
            + $"finished {FinishedEvents.Count + FinishedMissions.Count}";

        /// <summary>key 已平移到以今天为基准，所以 dlcSaveDate 记今天。</summary>
        public Carrier ToCarrier(int today) => new()
        {
            dlcSaveDate = today,
            scheduledEvents = Events,
            scheduledNews = News,
            scheduledNewsReplaceContents = ReplaceContents,
            allTrackingMissions = Missions,
            finishedEvents = FinishedEvents.ToArray(),
            finishedMissions = FinishedMissions.ToArray(),
        };
    }

    // Il2Cpp 集合不能直接用 LINQ。
    private static int Count<T>(IlDict.Dictionary<int, IlDict.List<T>> dict)
    {
        int n = 0;
        foreach (var kv in dict) n += kv.Value.Count;
        return n;
    }

    private static bool Contains(IlDict.Dictionary<int, IlDict.List<string>> dict, string label)
    {
        foreach (var kv in dict) if (kv.Value.Contains(label)) return true;
        return false;
    }

    private static bool Contains(IlDict.Dictionary<int, IlDict.List<RunTimeScheduler.TrackedMissionData>> dict, string label)
    {
        foreach (var kv in dict)
            foreach (var mission in kv.Value)
                if (mission.missionLabel == label) return true;
        return false;
    }

    private static IlDict.List<T> At<T>(IlDict.Dictionary<int, IlDict.List<T>> dict, int day)
    {
        if (!dict.TryGetValue(day, out var list))
        {
            list = new IlDict.List<T>();
            dict.Add(day, list);
        }
        return list;
    }
}

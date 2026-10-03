using System.Collections.Generic;
using System.Linq;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 任务节点领域注册器：持有任务节点配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// 节点表（<c>DataBaseScheduler.allNodes</c>）、节点映射与任务语言由框架按 <c>OnInjectMissionNodes</c> 写入；
/// 本类只保留标签查询：存档恢复（<c>SchedulerDataRecovery</c>）用它筛出属于本模组的节点。
/// </summary>
[AutoLog]
public static partial class MissionNodeRegistry
{
    private static readonly List<MissionNodeConfig> MissionNodeConfigs = new();

    internal static IEnumerable<MissionNodeConfig> Configs => MissionNodeConfigs;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.missionNodes == null) return;

        foreach (var missionNodeConfig in config.missionNodes)
        {
            MissionNodeConfigs.Add(missionNodeConfig);
            Log.LogInfo($"[{packageName}] Loaded config for mission node {missionNodeConfig.title}");
        }
    }

    public static List<string> GetAllMissionNodeLabels() => MissionNodeConfigs.Select(config => config.label).ToList();
}

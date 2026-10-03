using System.Collections.Generic;
using System.Linq;

using GameData.RunTime.Common;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 事件节点领域注册器：持有事件节点配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// 节点表与映射由框架按 <c>OnInjectEventNodes</c> 写入；本类保留羁绊事件激活与标签查询：
/// 前者在进入白天场景时按当前羁绊等级补排前置事件，后者供存档恢复筛出本模组的节点。
/// </summary>
[AutoLog]
public static partial class EventNodeRegistry
{
    private static readonly List<EventNodeConfig> EventNodeConfigs = new();

    internal static IEnumerable<EventNodeConfig> Configs => EventNodeConfigs;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.eventNodes == null) return;

        foreach (var eventNodeConfig in config.eventNodes)
        {
            EventNodeConfigs.Add(eventNodeConfig);
            Log.LogInfo($"[{packageName}] Loaded config for event node {eventNodeConfig.debugLabel}");
        }
    }

    internal static void ActivateAllKizunaEventNodes()
    {
        SpecialGuestRegistry.GetAllCharacterConfigs()
            .Where(c => c.kizuna != null)
            .ToList()
            .ForEach(CheckAndActivateKizunaEventNode);
        Log.Info("Kizuna event nodes activation completed.");
    }

    private static void CheckAndActivateKizunaEventNode(CharacterConfig config)
    {
        var currentBondLevel = RunTimeAlbum.RefOrGenerateSpecialRunTimeData(config.id).CurrentBondLevel;

        if (!TryGetPrerequisiteEventForBondLevel(config.kizuna, currentBondLevel, out var prerequisiteEvent))
        {
            if (currentBondLevel != 5)
            {
                Log.Error($"Invalid bond level {currentBondLevel} for character {config.name}({config.id})");
            }
            return;
        }

        if (RunTimeScheduler.scheduledEvents == null || !RunTimeScheduler.scheduledEvents.ContainsKey(-1))
        {
            Log.Error("RunTimeScheduler.scheduledEvents is null!");
            return;
        }
        var scheduledEvents = RunTimeScheduler.scheduledEvents[-1];
        if (scheduledEvents.Contains(prerequisiteEvent))
        {
            return;
        }

        if (RunTimeScheduler.finishedEvents.Contains(prerequisiteEvent))
        {
            Log.Info($"Kizuna event node for character {config.name}({config.id}) at bond level {currentBondLevel} already finished.");
            return;
        }

        if (prerequisiteEvent is null || prerequisiteEvent.Equals(""))
        {
            Log.Warning($"No prerequisite event defined for character {config.name}({config.id}) at bond level {currentBondLevel}, skipping activation.");
            return;
        }

        scheduledEvents.Add(prerequisiteEvent);
        Log.Info($"Activated kizuna event node for character {config.name}({config.id}) at bond level {currentBondLevel}");
    }

    private static bool TryGetPrerequisiteEventForBondLevel(KizunaEventConfig kizuna, int bondLevel, out string prerequisiteEvent)
    {
        prerequisiteEvent = bondLevel switch
        {
            1 => kizuna.lv1UpgradePrerequisiteEvent,
            2 => kizuna.lv2UpgradePrerequisiteEvent,
            3 => kizuna.lv3UpgradePrerequisiteEvent,
            4 => kizuna.lv4UpgradePrerequisiteEvent,
            _ => null
        };
        return prerequisiteEvent != null;
    }

    public static List<string> GetAllEventNodeLabels() => EventNodeConfigs.Select(config => config.label).ToList();
}

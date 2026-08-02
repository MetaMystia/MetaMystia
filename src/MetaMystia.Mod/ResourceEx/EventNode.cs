using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Collections;
using GameData.RunTime.Common;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia;


public static partial class ResourceExManager
{
    private static void ActivateAllKizunaEventNodes()
    {
        _characterConfigs.Values
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

    private static void RegisterAllEventNodes() => EventNodeConfigs.ToList().ForEach(RegisterEventNode);
    private static void RegisterEventNode(EventNodeConfig config)
    {
        var eventNode = config.ToEventNode();
        var success = DataBaseScheduler.allNodes.TryAdd(config.label, eventNode);
        Log.Info($"Registering EventNode {config.debugLabel}({config.label}), success: {success}");
    }


    private static void RegisterAllEventNodesMapping() => EventNodeConfigs.ToList().ForEach(RegisterEventNodeMapping);
    private static void RegisterEventNodeMapping(EventNodeConfig config)
    {
        try
        {
            DataBaseScheduler.AllNodesMapping[config.label] = "ResourceEx";
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register EventNode mapping for {config.label}: {ex.Message}");
        }
    }

    public static List<string> GetAllEventNodeLabels() => EventNodeConfigs.Select(config => config.label).ToList();
}

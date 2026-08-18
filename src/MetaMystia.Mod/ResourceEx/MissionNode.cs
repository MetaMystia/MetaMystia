using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia;


public static partial class ResourceExManager
{
    private static void RegisterAllMissionNodeLanguages() => MissionNodeConfigs.ToList().ForEach(RegisterMissionNodeLanguage);
    private static void RegisterMissionNodeLanguage(MissionNodeConfig config)
    {
        var lang = config.ToMissionLanguage();
        DataBaseLanguage.Missions.TryAdd(config.label, lang);
    }


    private static void RegisterAllMissionNodes() => MissionNodeConfigs.ToList().ForEach(RegisterMissionNode);
    private static void RegisterMissionNode(MissionNodeConfig config)
    {
        Log.Info($"Registering MissionNode {config.title}({config.debugLabel})");
        var missionNode = config.ToMissionNode();
        var success = DataBaseScheduler.allNodes.TryAdd(missionNode.label, missionNode);
        Log.Info($"Registered MissionNode {config.title}({config.label}): Success: {success}");
    }
    private static void RegisterAllMissionNodesMapping() => MissionNodeConfigs.ToList().ForEach(RegisterMissionNodeMapping);
    private static void RegisterMissionNodeMapping(MissionNodeConfig config)
    {
        try
        {
            DataBaseScheduler.AllNodesMapping[config.label] = "ResourceEx";
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register MissionNode mapping for {config.label}: {ex.Message}");
        }
    }

    public static List<string> GetAllMissionNodeLabels() => MissionNodeConfigs.Select(config => config.label).ToList();
}

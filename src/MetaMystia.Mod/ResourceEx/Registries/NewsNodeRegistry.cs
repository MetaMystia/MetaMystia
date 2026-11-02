using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 新闻节点领域注册器：持有新闻节点配置，负责注册与语言注册。排程与取消由原生 Reward 完成。
/// </summary>
[AutoLog]
public static partial class NewsNodeRegistry
{
    private static readonly List<NewsNodeConfig> NewsNodeConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.newsNodes == null) return;

        foreach (var newsNodeConfig in config.newsNodes)
        {
            NewsNodeConfigs.Add(newsNodeConfig);
            Log.LogInfo($"[{packageName}] Loaded config for news node {newsNodeConfig.title}({newsNodeConfig.label})");
        }
    }

    internal static void RegisterAllNewsNodeLanguages() => NewsNodeConfigs.ToList().ForEach(RegisterNewsNodeLanguage);
    private static void RegisterNewsNodeLanguage(NewsNodeConfig config)
    {
        var lang = config.ToNewsLanguage();
        DataBaseLanguage.News[config.label] = lang;
        Log.Info($"Registered NewsNode language {config.title}({config.label})");
    }

    internal static void RegisterAllNewsNodes() => NewsNodeConfigs.ToList().ForEach(RegisterNewsNode);
    private static void RegisterNewsNode(NewsNodeConfig config)
    {
        var newsNode = config.ToNewsNode();
        DataBaseScheduler.newsNodes[newsNode.label] = newsNode;
        Log.Info($"Registered NewsNode {config.title}({config.label})");
    }

    internal static void RegisterAllNewsNodesMapping() => NewsNodeConfigs.ToList().ForEach(RegisterNewsNodeMapping);
    private static void RegisterNewsNodeMapping(NewsNodeConfig config) => DataBaseScheduler.NewsNodesMapping[config.label] = "ResourceEx";

    public static List<string> GetAllNewsNodeLabels() => NewsNodeConfigs.Select(config => config.label).ToList();
}

using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.Profile;
using GameData.Profile.SchedulerNodeCollection;
using GameData.RunTime.Common;
using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia;

public static partial class ResourceExManager
{
    private static void RegisterAllNewsNodeLanguages() => NewsNodeConfigs.ToList().ForEach(RegisterNewsNodeLanguage);

    private static void RegisterNewsNodeLanguage(NewsNodeConfig config)
    {
        var lang = config.ToNewsLanguage();
        DataBaseLanguage.News[config.label] = lang;
        Log.Info($"Registered NewsNode language {config.title}({config.label})");
    }

    private static void RegisterAllNewsNodes() => NewsNodeConfigs.ToList().ForEach(RegisterNewsNode);

    private static void RegisterNewsNode(NewsNodeConfig config)
    {
        var newsNode = config.ToNewsNode();
        DataBaseScheduler.newsNodes[newsNode.label] = newsNode;
        Log.Info($"Registered NewsNode {config.title}({config.label})");
    }

    private static void RegisterAllNewsNodesMapping() => NewsNodeConfigs.ToList().ForEach(RegisterNewsNodeMapping);

    private static void RegisterNewsNodeMapping(NewsNodeConfig config)
    {
        try
        {
            DataBaseScheduler.NewsNodesMapping[config.label] = "ResourceEx";
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to register NewsNode mapping for {config.label}: {ex.Message}");
        }
    }

    public static List<string> GetAllNewsNodeLabels() => NewsNodeConfigs.Select(config => config.label).ToList();

    public static bool IsResourceNewsLoaded(string newsLabel) => GetAllNewsNodeLabels().Contains(newsLabel);

    public static bool ScheduleResourceNews(string newsLabel, SchedulerNode.Day targetDate, params RunTimeScheduler.HistoryNewsData.ReplaceContent[] replaceContents)
    {
        if (!IsResourceNewsLoaded(newsLabel))
        {
            Log.Warning($"Will not schedule unloaded ResourceEx news: {newsLabel}");
            return false;
        }

        RunTimeScheduler.ScheduleNews(newsLabel, targetDate, replaceContents ?? Array.Empty<RunTimeScheduler.HistoryNewsData.ReplaceContent>());
        return true;
    }

    public static bool ScheduleResourceNewsTomorrow(string newsLabel, params RunTimeScheduler.HistoryNewsData.ReplaceContent[] replaceContents)
    {
        var targetDate = new SchedulerNode.Day
        {
            dayType = SchedulerNode.Day.DayType.Absolute,
            dayCalcType = SchedulerNode.Day.CalculateType.Constant,
            day = RunTimePlayerData.GetDay().CorrectedDay + 1
        };
        return ScheduleResourceNews(newsLabel, targetDate, replaceContents);
    }

    public static bool DismissResourceNews(string newsLabel)
    {
        if (RunTimeScheduler.scheduledNews == null)
        {
            return false;
        }

        var removed = false;
        foreach (var newsList in RunTimeScheduler.scheduledNews.Values)
        {
            while (newsList.Remove(newsLabel))
            {
                removed = true;
            }
        }

        if (RunTimeScheduler.scheduledNewsReplaceContents != null)
        {
            foreach (var replaceList in RunTimeScheduler.scheduledNewsReplaceContents.Values)
            {
                for (var i = replaceList.Count - 1; i >= 0; i--)
                {
                    if (replaceList[i].Key == newsLabel)
                    {
                        replaceList.RemoveAt(i);
                        removed = true;
                    }
                }
            }
        }

        if (removed)
        {
            Log.Info($"Dismissed scheduled ResourceEx news: {newsLabel}");
        }
        return removed;
    }
}

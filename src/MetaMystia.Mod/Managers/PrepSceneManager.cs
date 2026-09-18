using System;
using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

namespace MetaMystia;

[AutoLog]
public static partial class PrepSceneManager
{
    public static UpdatePrepMessage.Table localPrepTable = new();
    private static bool prepInitialized;
    private static readonly List<UpdatePrepMessage> bufferedPrepUpdates = new();
    public static bool IsEditingPreset { get; set; }
    public static bool IsOpeningPanel { get; set; }
    public static bool CanSyncEdits => GameSession.IsInRoom && prepInitialized && !IsOpeningPanel
        && (IsYuyukoChallenge ? IsYuyukoPrepActive && !yuyukoPrepConfirmed
            : GameFlow.Destination == DayDestination.Business
                && GameFlow.LocalScene == Common.UI.Scene.IzakayaPrepScene && !completingPrep);

    public static void Initialize()
    {
        if (GameSession.HasRoomPeers)
            GameData.RunTime.Common.StatusTracker.Instance.partners.Clear();
    }

    public static void ClearPrepTable()
    {
        completingPrep = false;
        prepInitialized = false;
        IsEditingPreset = false;
        IsOpeningPanel = false;
        localPrepTable = new();
        bufferedPrepUpdates.Clear();
    }

    public static UpdatePrepMessage.Table CaptureTable()
    {
        var configure = IzakayaConfigure.Instance;
        var table = new UpdatePrepMessage.Table();
        foreach (var recipe in configure.DailyRecipes) table.Recipes.Add(recipe.Id);
        foreach (var beverage in configure.DailyBeverages) table.Beverages.Add(beverage.Id);
        for (int i = 0; i < Math.Min(configure.CookerConfigure.Length, table.Cookers.Length); i++)
            table.Cookers[i].Id = configure.CookerConfigure[i];
        return table;
    }

    public static void BeginPrep()
    {
        if (!GameSession.IsInRoom || (!IsYuyukoChallenge && GameFlow.Destination != DayDestination.Business)) return;
        prepInitialized = true;
        if (GameSession.IsRoomHost)
        {
            localPrepTable = CaptureTable();
            UpdatePrepMessage.Send(localPrepTable);
        }
        else UpdatePrepMessage.RequestState();
        FlushBufferedTables();
    }

    public static void FlushBufferedTables()
    {
        if (!prepInitialized) return;
        var pending = bufferedPrepUpdates.ToArray();
        bufferedPrepUpdates.Clear();
        foreach (var message in pending) ReceivePrepUpdate(message);
    }

    public static void ReceivePrepUpdate(UpdatePrepMessage message)
    {
        if (message.PrepRound > 0)
        {
            if (!GameFlow.IsFinalTrial) return;
            if (message.PrepRound == YuyukoPrepRound + 1)
            {
                bufferedPrepUpdates.Add(message);
                return;
            }
            if (message.PrepRound != YuyukoPrepRound || !IsYuyukoPrepActive || yuyukoPrepConfirmed) return;
        }
        else
        {
            if (GameFlow.Destination != DayDestination.Business || completingPrep) return;
            if (GameFlow.LocalScene != Common.UI.Scene.IzakayaPrepScene)
            {
                if (GameFlow.LocalScene is Common.UI.Scene.DayScene or Common.UI.Scene.LoadScene)
                    bufferedPrepUpdates.Add(message);
                return;
            }
        }
        if (!prepInitialized)
        {
            bufferedPrepUpdates.Add(message);
            return;
        }
        if (!GameSession.IsRoomHost)
        {
            ApplyHostTable(message.PrepTable);
            return;
        }

        var level = GameData.RunTime.Common.RunTimePlayerData.LevelProfile;
        ApplyItems(localPrepTable.Recipes, message.AddedRecipes, message.RemovedRecipes,
            level.MaxDailyRecipe, PlayerManager.RecipeAvailable, true);
        ApplyItems(localPrepTable.Beverages, message.AddedBeverages, message.RemovedBeverages,
            level.MaxDailyBev, PlayerManager.BeverageAvailable);
        foreach (var pair in message.ChangedCookers)
            if (pair.Key >= 0 && pair.Key < IzakayaConfigure.Instance.CookerConfigure.Length
                && pair.Key < localPrepTable.Cookers.Length
                && (pair.Value == -1 || PlayerManager.CookerAvailable(pair.Value)))
                localPrepTable.Cookers[pair.Key].Id = pair.Value;
        UpdateAll();
        UpdatePrepMessage.Send(localPrepTable);
    }

    private static void ApplyItems(List<int> items, int[] added, int[] removed, int limit, Func<int, bool> available, bool recipes = false)
    {
        foreach (int id in removed)
            if (!recipes || NightScene.NightSceneDirector.ChallengeMode != NightScene.NightSceneDirector.ChallengeType.NotChallenge
                || !GameData.RunTime.Common.RunTimePlayerData.CheckRecipeIsLocked(id)) items.Remove(id);
        foreach (int id in added)
            if (items.Count < limit && !items.Contains(id) && available(id)) items.Add(id);
    }

    public static void FinishLocalEdit(UpdatePrepMessage.Table before, bool preset = false)
    {
        if (before == null) return;
        var after = CaptureTable();
        // 原游戏负责库存和锁定检查，实际配置由主机结果覆盖。
        UpdateGroups();
        UpdatePrepMessage.Submit(before, after, preset);
    }

    public static UpdatePrepMessage.Table GetLocalPrepTableSnapshot() => localPrepTable.Clone();

    public static void ApplyHostTable(UpdatePrepMessage.Table table)
    {
        localPrepTable = table.Clone();
        UpdateAll();
    }

    public static void UpdateRecipes()
    {
        var items = IzakayaConfigure.Instance.DailyRecipes;
        items.Clear();
        foreach (int id in localPrepTable.Recipes)
            if (DataBaseCore.Recipes.TryGetValue(id, out var recipe)) items.Add(recipe);
    }

    public static void UpdateBeverages()
    {
        var items = IzakayaConfigure.Instance.DailyBeverages;
        items.Clear();
        foreach (int id in localPrepTable.Beverages)
            if (DataBaseCore.Beverages.TryGetValue(id, out var beverage)) items.Add(beverage);
    }

    public static void UpdateCookers()
    {
        var cookers = IzakayaConfigure.Instance.CookerConfigure;
        for (int i = 0; i < cookers.Length; i++)
            cookers[i] = i < localPrepTable.Cookers.Length ? localPrepTable.Cookers[i].Id : -1;
    }

    public static void UpdateGroups()
    {
        UpdateRecipes();
        UpdateBeverages();
        UpdateCookers();
    }

    public static void UpdateUI()
    {
        IzakayaConfigPannelPatch.instanceRef?.SolveDailyCompletion();
        IzakayaConfigPannelPatch.instanceRef?.m_CookerGroup?.UpdateGroupRaw();
        IzakayaConfigPannelPatch.instanceRef?.m_BeverageGroup?.UpdateGroupRaw();
        IzakayaConfigPannelPatch.instanceRef?.m_RecipeGroup?.UpdateGroupRaw();
    }

    public static void UpdateAll()
    {
        UpdateGroups();
        UpdateUI();
    }
}

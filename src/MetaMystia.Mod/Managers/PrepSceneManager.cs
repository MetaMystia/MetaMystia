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
    public static bool IsOpeningPanel { get; set; }
    public static bool CanSubmitEdits => GameSession.IsInRoom
        && (IsYuyukoChallenge ? IsOpeningPanel || (IsYuyukoPrepActive && !yuyukoPrepConfirmed)
            : GameFlow.Destination == DayDestination.Business && !completingPrep
                && (IsOpeningPanel || GameFlow.LocalScene is Common.UI.Scene.DayScene or Common.UI.Scene.LoadScene or Common.UI.Scene.IzakayaPrepScene));
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
        IsOpeningPanel = false;
        localPrepTable = new();
        bufferedPrepUpdates.Clear();
    }

    public static void BeginPrep()
    {
        if (!GameSession.IsInRoom || (!IsYuyukoChallenge && GameFlow.Destination != DayDestination.Business)) return;
        prepInitialized = true;
        // 最终试炼沿用本轮开始前的配置，再处理本轮逐项修改。
        if (IsYuyukoChallenge && GameSession.IsRoomHost)
        {
            var configure = IzakayaConfigure.Instance;
            localPrepTable = new();
            foreach (var recipe in configure.DailyRecipes) localPrepTable.Recipes.Add(recipe.Id);
            foreach (var beverage in configure.DailyBeverages) localPrepTable.Beverages.Add(beverage.Id);
            for (int i = 0; i < Math.Min(configure.CookerConfigure.Length, localPrepTable.Cookers.Length); i++)
                localPrepTable.Cookers[i].Id = configure.CookerConfigure[i];
            UpdatePrepMessage.Send(localPrepTable);
        }
        FlushBufferedTables();
        if (!GameSession.IsRoomHost) UpdatePrepMessage.RequestState();
    }

    public static void FlushBufferedTables()
    {
        if (!prepInitialized) return;
        var pending = bufferedPrepUpdates.ToArray();
        bufferedPrepUpdates.Clear();
        foreach (var message in pending) ReceivePrepUpdate(message);
    }

    public static void ReceivePrepUpdate(UpdatePrepMessage message, bool updateMenu = true)
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
        if (!prepInitialized || IsOpeningPanel)
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
        if (updateMenu) UpdateAll();
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

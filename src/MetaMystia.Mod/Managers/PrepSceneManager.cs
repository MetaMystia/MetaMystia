using System;
using System.Collections.Generic;

using GameData.Core.Collections;

using MetaMystia.Network;
using MetaMystia.Patch;


namespace MetaMystia;

[AutoLog]
public static partial class PrepSceneManager
{
    public static UpdatePrepAction.Table localPrepTable = new();

    public static readonly int MaxRecipes = 8;
    public static readonly int MaxBeverages = 8;
    public static readonly int MaxCookers = 8; // 可信联机下双方都不会越界

    public static void Initialize()
    {
        if (!MpManager.IsConnected)
        {
            return;
        }
        GameData.RunTime.Common.StatusTracker.Instance.partners.Clear();
    }

    public static void ClearPrepTable() => localPrepTable = new UpdatePrepAction.Table();

    public static UpdatePrepAction.Table GetLocalPrepTableSnapshot() => localPrepTable.Clone();

    /// <summary>客机：放弃本地备菜修改，强制应用主机权威表。</summary>
    public static void ApplyHostTable(UpdatePrepAction.Table hostTable)
    {
        localPrepTable = hostTable?.Clone() ?? new UpdatePrepAction.Table();
        Log.LogInfo("Applied authoritative prep table from host.");
        UpdateAll();
    }

    public static void MergeFromPeer(UpdatePrepAction.Table remotePrepTable)
    {
        bool changed = false;

        changed |= MergeDictionary(localPrepTable.RecipeAdditions, remotePrepTable.RecipeAdditions);
        changed |= MergeDictionary(localPrepTable.RecipeDeletions, remotePrepTable.RecipeDeletions);

        changed |= MergeDictionary(localPrepTable.BeverageAdditions, remotePrepTable.BeverageAdditions);
        changed |= MergeDictionary(localPrepTable.BeverageDeletions, remotePrepTable.BeverageDeletions);

        changed |= MergeCookers(remotePrepTable);

        // Check limits and trim if necessary
        changed |= CheckAndTrimLimit(localPrepTable.RecipeAdditions, localPrepTable.RecipeDeletions, MaxRecipes);
        changed |= CheckAndTrimLimit(localPrepTable.BeverageAdditions, localPrepTable.BeverageDeletions, MaxBeverages);

        if (changed)
        {
            Log.LogInfo($"Merged from peer, state changed.");
            UpdateAll();
        }
    }

    private static bool CheckAndTrimLimit(Dictionary<int, long> additions, Dictionary<int, long> deletions, int limit)
    {
        bool changed = false;
        // Find valid items
        var validItems = new List<KeyValuePair<int, long>>();
        foreach (var kvp in additions)
        {
            int id = kvp.Key;
            long addTs = kvp.Value;
            long delTs = deletions.ContainsKey(id) ? deletions[id] : 0;

            if (addTs > delTs)
            {
                validItems.Add(kvp);
            }
        }

        if (validItems.Count > limit)
        {
            // Sort by timestamp descending (latest first)
            validItems.Sort((a, b) => b.Value.CompareTo(a.Value));

            // Remove the latest items until count <= limit
            int removeCount = validItems.Count - limit;
            for (int i = 0; i < removeCount; i++)
            {
                var itemToRemove = validItems[i];
                deletions[itemToRemove.Key] = MpManager.GetSynchronizedTimestampNow;
                changed = true;
                Log.LogInfo($"Trimmed item {itemToRemove.Key} due to limit.");
            }
        }
        return changed;
    }

    private static bool MergeDictionary(Dictionary<int, long> local, Dictionary<int, long> remote)
    {
        bool changed = false;
        foreach (var kvp in remote)
        {
            int id = kvp.Key;
            long ts = kvp.Value;
            if (!local.ContainsKey(id) || ts > local[id])
            {
                local[id] = ts;
                changed = true;
            }
        }
        return changed;
    }

    private static bool MergeCookers(UpdatePrepAction.Table remotePrepTable)
    {
        if (remotePrepTable == null)
        {
            return false;
        }

        var remoteSlots = NormalizeCookerSlots(remotePrepTable.Cookers);
        var localSlots = EnsureLocalCookerSlots();

        bool changed = false;
        for (int i = 0; i < localSlots.Length; i++)
        {
            var remoteSlot = remoteSlots[i];
            var localSlot = localSlots[i];

            if (remoteSlot.Timestamp > localSlot.Timestamp ||
                (remoteSlot.Timestamp == localSlot.Timestamp && remoteSlot.Id != localSlot.Id))
            {
                localSlot.Id = remoteSlot.Id;
                localSlot.Timestamp = remoteSlot.Timestamp;
                changed = true;
            }
        }

        return changed;
    }

    internal static CookerSlot[] GetLocalCookerSlots()
    {
        return EnsureLocalCookerSlots();
    }

    private static CookerSlot[] EnsureLocalCookerSlots()
    {
        if (localPrepTable.Cookers == null)
        {
            localPrepTable.Cookers = CookerSlot.CreateDefaultArray();
        }

        var slots = localPrepTable.Cookers;
        if (slots.Length != CookerSlot.SlotsLength)
        {
            var normalized = CookerSlot.CreateDefaultArray();
            int limit = Math.Min(slots.Length, normalized.Length);
            for (int i = 0; i < limit; i++)
            {
                if (slots[i] != null)
                {
                    normalized[i].Id = slots[i].Id;
                    normalized[i].Timestamp = slots[i].Timestamp;
                }
            }

            localPrepTable.Cookers = normalized;
            slots = normalized;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new CookerSlot();
            }
        }

        return slots;
    }

    private static CookerSlot[] NormalizeCookerSlots(CookerSlot[] source)
    {
        var normalized = CookerSlot.CreateDefaultArray();
        if (source == null)
        {
            return normalized;
        }

        int limit = Math.Min(source.Length, normalized.Length);
        for (int i = 0; i < limit; i++)
        {
            var slot = source[i];
            if (slot != null)
            {
                normalized[i].Id = slot.Id;
                normalized[i].Timestamp = slot.Timestamp;
            }
        }

        return normalized;
    }

    private static void UpdateItems<T>(
        Il2CppSystem.Collections.Generic.List<T> dailyList,
        string listName,
        Dictionary<int, long> additions,
        Dictionary<int, long> deletions,
        Il2CppSystem.Collections.Generic.Dictionary<int, T> allItems,
        string itemTypeName) where T : class
    {
        if (dailyList == null)
        {
            Log.LogError($"{listName} list is null!");
            return;
        }

        if (allItems == null) return;

        // Filter valid items from localPrepTable
        var validItems = new List<KeyValuePair<int, long>>();
        foreach (var kvp in additions)
        {
            int id = kvp.Key;
            long addTs = kvp.Value;
            long delTs = deletions.ContainsKey(id) ? deletions[id] : 0;

            if (addTs > delTs)
            {
                validItems.Add(kvp);
            }
        }

        // Sort by timestamp ascending
        validItems.Sort((a, b) => a.Value.CompareTo(b.Value));

        // Update daily list
        dailyList.Clear();

        foreach (var kvp in validItems)
        {
            if (allItems.TryGetValue(kvp.Key, out var item))
            {
                dailyList.Add(item);
            }
            else
            {
                Log.LogWarning($"{itemTypeName} with ID {kvp.Key} not found in GameData.Core.Collections.DataBaseCore.{itemTypeName}s");
            }
        }

        Log.LogInfo($"Updated {listName} with {dailyList.Count} items.");
    }

    public static void UpdateRecipes()
    {
        UpdateItems<Recipe>(
            GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.DailyRecipes,
            "DailyRecipes",
            localPrepTable.RecipeAdditions,
            localPrepTable.RecipeDeletions,
            DataBaseCore.Recipes,
            "Recipe"
        );
    }

    public static void UpdateBeverages()
    {
        UpdateItems<Sellable>(
            GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.DailyBeverages,
            "DailyBeverages",
            localPrepTable.BeverageAdditions,
            localPrepTable.BeverageDeletions,
            DataBaseCore.Beverages,
            "Beverage"
        );
    }
    public static void UpdateCookers()
    {
        var cookerConfigure = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.CookerConfigure;
        if (cookerConfigure == null)
        {
            Log.LogError($"CookerConfigure array is null!");
            return;
        }

        var sourceSlots = EnsureLocalCookerSlots();

        int usableLength = cookerConfigure.Length; // 该数组长度即为实际可用长度(3/6/8) // 20260128注: 特殊场景如 博丽大祭 可能有 不同情况如 10 个

        for (int i = 0; i < usableLength; i++)
        {
            cookerConfigure[i] = sourceSlots[i].Id;
        }

        for (int i = usableLength; i < cookerConfigure.Length; i++)
        {
            cookerConfigure[i] = -1;
        }

        int activeCount = 0;
        for (int i = 0; i < usableLength; i++)
        {
            if (cookerConfigure[i] >= 0)
            {
                activeCount++;
            }
        }

        Log.LogInfo($"Updated cookersList with {activeCount} active slots (limit {usableLength}).");
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

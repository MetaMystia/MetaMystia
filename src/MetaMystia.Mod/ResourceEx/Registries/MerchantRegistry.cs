using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime.InteropTypes.Arrays;

using GameData.Core.Collections.DaySceneUtility;
using GameData.Core.Collections.DaySceneUtility.Collections;
using GameData.Profile;
using GameData.RunTime.DaySceneUtility;

using static GameData.Core.Collections.DaySceneUtility.Collections.Merchant;

using MetaMystia.ResourceEx.Mappers;
using MetaMystia.ResourceEx.Models;
using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 商人领域注册器：持有商人配置与构建产物，负责构建、追踪与清理。
/// </summary>
[AutoLog]
public static partial class MerchantRegistry
{
    private static readonly Dictionary<string, MerchantConfig> MerchantConfigs = new();
    private static readonly Dictionary<string, Merchant> _builtMerchants = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.merchants == null) return;

        foreach (var merchantConfig in config.merchants)
        {
            MerchantConfigs[merchantConfig.key] = merchantConfig;
            Log.LogInfo($"[{packageName}] Loaded config for merchant {merchantConfig.key}");
        }
    }

    public static bool IsResourceExSpecialMerchant(this string stringId, string type = "Special") =>
        stringId.IsResourceExSpecialGuest() && _builtMerchants.ContainsKey(stringId);

    /// <summary>
    /// Removes orphaned tracked merchant entries from RunTimeDayScene.trackedMerchants
    /// that no longer have a corresponding merchant definition in either the base game
    /// (DataBaseDay.allMerchants) or the current ResourceEx merchant configs.
    /// This prevents KeyNotFoundException when the game calls DataBaseDay.RefMerchant
    /// for a merchant whose resource pack has been removed.
    /// </summary>
    internal static void CheckAndCleanOrphanedMerchants()
    {
        var trackedMerchants = RunTimeDayScene.trackedMerchants;
        if (trackedMerchants == null) return;

        var orphanedKeys = new List<string>();
        foreach (var kvp in trackedMerchants)
        {
            var key = kvp.Key;
            // Check if the key exists in base game merchants or current ResourceEx merchants
            if (!DataBaseDay.allMerchants.ContainsKey(key) && !MerchantConfigs.ContainsKey(key))
            {
                orphanedKeys.Add(key);
            }
        }

        foreach (var key in orphanedKeys)
        {
            trackedMerchants.Remove(key);
            Log.Warning($"Removed orphaned tracked merchant: {key} (merchant definition no longer exists)");
        }

        if (orphanedKeys.Count > 0)
            Log.Info($"Cleaned up {orphanedKeys.Count} orphaned tracked merchant(s).");
    }

    internal static void RegisterAllTrackedMerchant()
    {
        Log.Info("Registering all tracked merchants...");
        MerchantConfigs.Values.ToList().ForEach(RegisterTrackedMerchant);
    }

    private static void RegisterTrackedMerchant(MerchantConfig config)
    {
        RunTimeDayScene.trackedMerchants[config.key] = config.GenTrackedMerchant();
        Log.Info($"Registered tracked merchant {config.key} with {config.merchandise.Count} products.");
    }

    public static void BuildAllMerchants() => MerchantConfigs.Values.ToList().ForEach(BuildMerchant);

    public static void BuildMerchant(MerchantConfig config)
    {
        var newMerchant = DataBaseDay.allMerchants.Values.GetEnumerator().Current;

        newMerchant.key = config.key;

        newMerchant.welcomeDialogPackage = config.welcomeDialogPackageNames.Select(DialogRegistry.GetBuiltDialogPackage).ToIl2CppReferenceArray();
        newMerchant.nullDialogPackage = config.nullDialogPackageNames.Select(DialogRegistry.GetBuiltDialogPackage).ToIl2CppReferenceArray();
        newMerchant.priceMultiplierRange = new UnityEngine.Vector2(config.priceMultiplierMin, config.priceMultiplierMax);
        newMerchant.leastSellNum = config.leastSellNum;

        newMerchant.merchandiseCollection = config.merchandise.Select(m => m.ToMerchandise()).ToIl2CppReferenceArray();

        _builtMerchants[config.key] = newMerchant;
        // DataBaseDay.allMerchants[config.key] = newMerchant; // do NOT directly modify the original dictionary
        Log.Info($"Built merchant {config.key}.");
    }

    public static bool TryGetExMerchantData(string key, out Merchant merchant)
    {
        if (_builtMerchants.TryGetValue(key, out merchant))
        {
            return true;
        }
        merchant = default;
        return false;
    }
}

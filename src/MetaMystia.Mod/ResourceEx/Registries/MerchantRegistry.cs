using System.Collections.Generic;

using GameData.Core.Collections.DaySceneUtility;
using GameData.RunTime.DaySceneUtility;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 商人领域注册器：持有资源包声明的商人配置，并提供查询口与孤儿追踪记录清理。
/// <para>商人定义（<c>DataBaseDay.allMerchants</c>）、运行时追踪记录（<c>RunTimeDayScene.trackedMerchants</c>，
/// 含商品生成与「已拥有食谱过滤」）与缺键安全由框架在注入时一次建立（<c>MerchantPipeline</c>）；
/// 本类不再写商人表，也不再手写追踪记录。</para>
/// </summary>
[AutoLog]
public static partial class MerchantRegistry
{
    private static readonly Dictionary<string, MerchantConfig> MerchantConfigs = new();

    /// <summary>资源包声明的商人配置；<c>ModDatabaseExtension.OnInjectMerchants</c> 由此映射为框架数据。</summary>
    internal static IReadOnlyCollection<MerchantConfig> Configs => MerchantConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.merchants == null) return;

        foreach (var merchantConfig in config.merchants)
        {
            MerchantConfigs[merchantConfig.key] = merchantConfig;
            Log.LogInfo($"[{packageName}] Loaded config for merchant {merchantConfig.key}");
        }
    }

    /// <summary>
    /// 是否为 ResourceEx 商人：稀客身份 + 游戏原表 <c>DataBaseDay.allMerchants</c> 中存在商人定义（由框架写入）。
    /// 迁移前这里查模组自建的商人表，改为查游戏原表后与游戏自身的 <c>IsMerchant</c> 语义一致。
    /// </summary>
    public static bool IsResourceExSpecialMerchant(this string stringId, string type = "Special") =>
        stringId.IsResourceExSpecialGuest() && stringId.IsMerchant();

    /// <summary>
    /// 移除 <c>RunTimeDayScene.trackedMerchants</c> 中已失去商人定义的孤儿记录：
    /// 既不在游戏原表 <c>DataBaseDay.allMerchants</c>（含框架写入的现役商人）也不在当前资源包配置里的键会被删除，
    /// 避免游戏调用 <c>DataBaseDay.RefMerchant</c> 时抛 KeyNotFoundException（存档里可能残留已移除资源包的商人）。
    /// 框架的 <c>MerchantPipeline.CleanOrphanedMerchants</c> 是 internal，未出现在 <c>Mystia.Net.Sdk</c> 公开面，
    /// 因此保留模组实现，并在进入白天场景时由 <c>ResourceExManager.OnDaySceneAwake</c> 调用。
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
}

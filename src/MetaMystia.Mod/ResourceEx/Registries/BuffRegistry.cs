using System.Collections.Generic;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// buff 注册器：持有资源包声明的 buff 标题、说明与图标配置，供 <c>ModDatabaseExtension</c> 注入框架数据面。
/// 与符卡等使用方解耦，使用方按 id 引用。图标规格见 docs/spell-creation/art-pipeline.md。
/// </summary>
[AutoLog]
public static partial class BuffRegistry
{
    private static readonly Dictionary<int, BuffConfig> BuffConfigs = [];

    internal static IEnumerable<BuffConfig> Configs => BuffConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.buffs == null) return;

        foreach (var buffConfig in config.buffs)
        {
            BuffConfigs[buffConfig.id] = buffConfig;
            Log.LogInfo($"[{packageName}] Loaded config for buff {buffConfig.id}");
        }
    }

    internal static bool IsAvailable(int id) =>
        BuffConfigs.TryGetValue(id, out var config)
        && !string.IsNullOrWhiteSpace(config.name)
        && !string.IsNullOrWhiteSpace(config.description)
        && !string.IsNullOrWhiteSpace(config.icon)
        && RexAssetRegistry.TryGetSprite(config.icon, out _);
}

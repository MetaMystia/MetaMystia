using System.Collections.Generic;

using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// buff 注册器：把资源包声明的 buff 标题、说明与图标写入 BuffDescription。
/// 与符卡等使用方解耦，使用方按 id 引用。图标规格见 docs/spell-creation/art-pipeline.md。
/// </summary>
[AutoLog]
public static partial class BuffRegistry
{
    private static readonly Dictionary<int, BuffConfig> BuffConfigs = [];

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

    internal static void RegisterAllBuffLanguages()
    {
        foreach (var config in BuffConfigs.Values)
        {
            if (!RexAssetRegistry.TryGetSprite(config.icon, out var icon))
                Log.LogError($"buff {config.id} 图标加载失败: {config.icon}");

            DataBaseLanguage.BuffDescription[(EventManager.BuffType)config.id] =
                new ObjectLanguageBase(config.name, config.description, icon);
        }
    }
}

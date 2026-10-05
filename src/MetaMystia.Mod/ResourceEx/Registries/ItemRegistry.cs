using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 物品领域注册器：合并 items、clothes、decorations，统一处理物品 ID 与语言注册。
/// 服装专属的 ClothesProfile、像素精灵与立绘由 <see cref="ClothRegistry"/> 注册。
/// </summary>
[AutoLog]
public static partial class ItemRegistry
{
    private static readonly Dictionary<int, ItemConfig> ItemConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        foreach (var itemConfig in (config?.items ?? []).Concat(config?.clothes ?? []).Concat(config?.decorations ?? []))
        {
            if (ItemConfigs.ContainsKey(itemConfig.id))
                Log.LogWarning($"[{packageName}] Item ID {itemConfig.id} ({itemConfig.name}) overrides a previously loaded item");

            ItemConfigs[itemConfig.id] = itemConfig;
            Log.LogInfo($"[{packageName}] Loaded config for item {itemConfig.name} ({itemConfig.id})");
        }
    }

    // DataBaseCore 初始化后
    internal static void RegisterAllItems()
    {
        foreach (var config in ItemConfigs.Values)
        {
            if (config is DecorationConfig decorationConfig)
            {
                var decoration = DecorationRegistry.CreateDecoration(decorationConfig);
                if (decoration == null) continue;
                DataBaseCore.Items[config.id] = decoration;
                DataBaseCore.Decorations[config.id] = decoration;
            }
            else
                DataBaseCore.Items[config.id] = new Item(config.id);
            Log.Info($"Registered Item ID {config.id} ({config.name})");
        }
    }

    // DataBaseLanguage 初始化后
    internal static void RegisterAllItemLanguages()
    {
        foreach (var config in ItemConfigs.Values)
        {
            if (config is DecorationConfig && !DataBaseCore.Decorations.ContainsKey(config.id)) continue;
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(config.spritePath) && !RexAssetRegistry.TryGetSprite(config.spritePath, out sprite))
                Log.LogWarning($"Item ID {config.id} ({config.name}) sprite not found: {config.spritePath}");

            DataBaseLanguage.Items[config.id] = new GameData.CoreLanguage.ObjectLanguageBase(
                name: config.name,
                Description: config.description ?? "",
                visual: sprite);
            Log.Info($"Registered item language ID {config.id} ({config.name})");
        }
    }
}

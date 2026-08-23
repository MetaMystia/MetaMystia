using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.CoreLanguage.Collections;

using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 服装领域注册器：持有服装配置，负责 Item / ClothesProfile / 语言 / 像素精灵注册与立绘查询。
/// </summary>
[AutoLog]
public static partial class ClothRegistry
{
    private static readonly Dictionary<int, ClothConfig> ClothConfigs = new();
    // Cloth portrait cache: clothId -> Sprite (loaded lazily or during preload)
    private static readonly Dictionary<int, Sprite> _clothPortraitCache = new();
    // Cloth pixel full cache: skinIndex -> CharacterSpriteSetFull (built during character init)
    private static readonly Dictionary<int, CharacterSpriteSetFull> _clothPixelFullCache = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.clothes == null) return;

        foreach (var clothConfig in config.clothes)
        {
            ClothConfigs[clothConfig.id] = clothConfig;
            Log.LogInfo($"[{packageName}] Loaded config for cloth {clothConfig.name} ({clothConfig.id})");
        }
    }

    /// <summary>
    /// 判断一个服装ID是否由 ResourceEx 注册
    /// </summary>
    public static bool IsResourceExCloth(int clothId) => ClothConfigs.ContainsKey(clothId);

    /// <summary>
    /// 获取 ResourceEx 注册的服装立绘 Sprite（用于 SetupPortrayalVisual 中动态替换）
    /// </summary>
    public static bool TryGetClothPortrait(int clothId, out Sprite portrait)
    {
        if (_clothPortraitCache.TryGetValue(clothId, out portrait))
            return portrait != null;

        if (!ClothConfigs.TryGetValue(clothId, out var config) || string.IsNullOrEmpty(config.portraitPath))
        {
            portrait = null;
            return false;
        }

        if (!RexAssetRegistry.TryGetSprite(config.portraitPath, out portrait))
            portrait = null;

        _clothPortraitCache[clothId] = portrait;
        return portrait != null;
    }


    // ========== Part 1: Item 注册 (DataBaseCore 初始化后) ==========

    internal static void RegisterAllClothItems()
    {
        Log.Info("Registering all cloth Items from ResourceEx...");
        foreach (var config in ClothConfigs.Values)
        {
            RegisterClothItem(config);
        }
    }

    private static void RegisterClothItem(ClothConfig config)
    {
        var item = new Item(config.id);
        DataBaseCore.Items[config.id] = item;
        Log.Info($"Registered cloth Item ID {config.id} ({config.name})");
    }


    // ========== Part 2: ClothesProfile 注册 (DataBaseCore 初始化后) ==========

    internal static void RegisterAllClothProfiles()
    {
        Log.Info("Registering all cloth profiles from ResourceEx...");

        // 按 ID 排序分配 skinIndex，因为 dlcs[] 是空的，索引从 0 开始依次分配
        var sortedConfigs = ClothConfigs.Values.OrderBy(c => c.id).ToList();

        for (int i = 0; i < sortedConfigs.Count; i++)
        {
            RegisterClothProfile(sortedConfigs[i], skinDlcIndex: i);
        }
    }

    private static void RegisterClothProfile(ClothConfig config, int skinDlcIndex)
    {
        // 使用已有服装作为模板获取必需的 OverrideVisualAsset 引用
        var templateClothProfile = DataBaseCore.Clothes[-1];

        var clothProfile = new GameData.Profile.ClothesProfile.Clothes()
        {
            index = config.id,
            frameTime = 0f,
            izakayaSkinIndex = config.izakayaSkinIndex,
            izkayaHorizontalOffset = config.izkayaHorizontalOffset,
            m_OverrideDynamicVisualAsset = null,
            m_OverrideVisualAsset = templateClothProfile.m_OverrideVisualAsset,
            notebookHorizontalOffset = config.notebookHorizontalOffset,
            notebookVerticalOffset = config.notebookVerticalOffset,
            notebookUITitleOffset = new UnityEngine.Vector2(config.notebookUITitleHorizontalOffset, config.notebookUITitleVerticalOffset),
            skinIndex = new CharacterSkinSets.SkinSelectionInfo()
            {
                index = skinDlcIndex,
                selectedType = CharacterSkinSets.SelectedType.DLC
            },
        };

        DataBaseCore.Clothes[config.id] = clothProfile;
        Log.Info($"Registered cloth profile ID {config.id} ({config.name}), skinDlcIndex={skinDlcIndex}");
    }


    // ========== Part 3: Language 注册 (DataBaseLanguage 初始化后) ==========

    internal static void RegisterAllClothLanguages()
    {
        Log.Info("Registering all cloth languages from ResourceEx...");
        foreach (var config in ClothConfigs.Values)
        {
            RegisterClothLanguage(config);
        }
    }

    private static void RegisterClothLanguage(ClothConfig config)
    {
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(config.spritePath))
        {
            RexAssetRegistry.TryGetSprite(config.spritePath, out sprite);
        }

        var lang = new GameData.CoreLanguage.ObjectLanguageBase(
            name: config.name,
            Description: config.description ?? "",
            visual: sprite);

        DataBaseLanguage.Items[config.id] = lang;
        Log.Info($"Registered cloth language ID {config.id} ({config.name})");
    }


    // ========== Part 4: 像素精灵注册 (DataBaseCharacter 初始化后) ==========

    internal static void RegisterAllClothPixelSprites()
    {
        Log.Info("Registering all cloth pixel sprites to SelfSpriteSet.dlcs...");

        if (ClothConfigs.Count == 0)
        {
            Log.Info("No cloth configs to register.");
            return;
        }

        // 按 ID 排序，与 RegisterAllClothProfiles 中的顺序一致，保证 skinIndex 对应
        var sortedConfigs = ClothConfigs.Values.OrderBy(c => c.id).ToList();

        // 构建 dlcs 数组
        var dlcsArray = new Il2CppReferenceArray<CharacterSpriteSetCompact>(sortedConfigs.Count);

        for (int i = 0; i < sortedConfigs.Count; i++)
        {
            var config = sortedConfigs[i];
            if (config.pixelFullConfig != null)
            {
                var pixelFull = PixelSpriteFactory.MakePixelFull(config.pixelFullConfig);
                _clothPixelFullCache[i] = pixelFull;
                dlcsArray[i] = pixelFull; // CharacterSpriteSetFull 继承自 CharacterSpriteSetCompact
                Log.Info($"Built cloth pixel full for ID {config.id} ({config.name}), dlcIndex={i}");
            }
            else
            {
                Log.LogWarning($"Cloth ID {config.id} ({config.name}) has no pixelFullConfig, using fallback.");
                dlcsArray[i] = DataBaseCharacter.FallbackFullPixel;
            }
        }

        DataBaseCharacter.SelfSpriteSet.dlcs = dlcsArray;
        Log.Info($"Registered {sortedConfigs.Count} cloth pixel sprites to SelfSpriteSet.dlcs");
    }
}

using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime.Injection;
using UnityEngine;

using Common.CharacterUtility;
using Common.UI;
using GameData.Core.Collections;
using GameData.RunTime.Common;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.DecorationCollection;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>创建装饰效果与原版对象；物品配置合并、ID 冲突及语言由 ItemRegistry 统一处理。</summary>
[AutoLog]
public static partial class DecorationRegistry
{
    private static readonly Dictionary<string, Func<DecorationConfig, DecorationBaseEx>> Implementations = new()
    {
        ["DovePendant"] = Create<DovePendantDecoration>,
        ["DarumaYukiyuki"] = Create<DarumaYukiyukiDecoration>,
    };

    private static readonly Dictionary<int, DecorationBaseEx> Effects = [];
    private static readonly HashSet<int> DayEquipped = [];
    private static CharacterControllerUnit _dayPlayer;

    internal static Decoration CreateDecoration(DecorationConfig config)
    {
        if (!Implementations.TryGetValue(config.implementation ?? string.Empty, out var create))
        {
            Log.LogWarning($"装饰 {config.id} 的实现 {config.implementation} 不存在，跳过注册");
            return null;
        }
        if (!Enum.IsDefined(config.decorationType))
        {
            Log.LogWarning($"装饰 {config.id} 的类型 {config.decorationType} 无效，跳过注册");
            return null;
        }
        if (!RexAssetRegistry.TryGetSprite(config.spritePath, out var sprite))
        {
            Log.LogWarning($"装饰 {config.id} 图标加载失败: {config.spritePath}，跳过注册");
            return null;
        }

        var effect = create(config);
        if (effect == null) return null;
        effect.Config = config;
        Effects[config.id] = effect;
        return new Decoration(config.id, sprite, effect, config.decorationType, config.conflictDecorationIds ?? []);
    }

    private static DecorationBaseEx Create<T>(DecorationConfig config) where T : DecorationBaseEx, IDecorationDependencies
    {
        if (T.CheckDependencies(config) is { } reason)
        {
            Log.LogWarning($"装饰 {config.id} {reason}，跳过注册");
            return null;
        }
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return ScriptableObject.CreateInstance<T>();
    }

    internal static void RefreshDayEffects()
    {
        if (GameFlow.LocalScene != Scene.DayScene) return;
        var player = DayScene.SceneManager.Instance?.Character?.Character;
        if (player == null) return;

        if (_dayPlayer != player)
        {
            if (_dayPlayer != null)
                foreach (var id in DayEquipped)
                    Effects[id].OnDayUnequip(_dayPlayer);
            DayEquipped.Clear();
            _dayPlayer = player;
        }

        // 原版装备方法会直接移除冲突项，需核对最终集合，不能只处理传入的 ID。
        foreach (var (id, effect) in Effects)
        {
            if (RunTimeAlbum.HasDecorationUsing(id))
            {
                if (DayEquipped.Add(id)) effect.OnDayEquip(player);
            }
            else if (DayEquipped.Remove(id))
                effect.OnDayUnequip(player);
        }
    }
}

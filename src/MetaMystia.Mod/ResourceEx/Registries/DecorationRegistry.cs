using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime.Injection;
using UnityEngine;

using Common.UI;
using GameData.Core.Collections;
using GameData.RunTime.Common;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.DecorationCollection;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 装饰注册器：按资源包 decorations 声明创建对应的代码实现与原版 Decoration，并汇总白天效果。
/// 物品配置合并、ID 冲突及语言由 ItemRegistry 统一处理。本类不含任何具体装饰的内容。
/// </summary>
[AutoLog]
public static partial class DecorationRegistry
{
    /// <summary>implementation → 代码实现。新增装饰实现时在此加一行。</summary>
    private static readonly Dictionary<string, Func<int, DecorationBaseEx>> Implementations = new()
    {
        ["DovePendant"] = Create<DovePendantDecoration>,
        ["DarumaYukiyuki"] = Create<DarumaYukiyukiDecoration>,
    };

    private static readonly Dictionary<int, DecorationBaseEx> Effects = [];

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

        var effect = create(config.id);
        if (effect == null) return null;
        Effects[config.id] = effect;
        return new Decoration(config.id, sprite, effect, config.decorationType, config.conflictDecorationIds ?? []);
    }

    private static DecorationBaseEx Create<T>(int id) where T : DecorationBaseEx, IDecorationDependencies
    {
        if (T.CheckDependencies() is { } reason)
        {
            Log.LogWarning($"装饰 {id} {reason}，跳过注册");
            return null;
        }

        // ClassInjector 必须先于 CreateInstance；同一实现可被多个装饰复用，只注入一次。
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return ScriptableObject.CreateInstance<T>();
    }

    /// <summary>
    /// 按当前装备集合重算白天效果并直接赋值，可重复调用。原版在角色初始化、
    /// 挑战存档挂载时把移速重置为等级值，调用方在这些时点及装备变化后调用。
    /// </summary>
    internal static void ApplyDayEffects()
    {
        if (Effects.Count == 0 || GameFlow.LocalScene != Scene.DayScene) return;
        var player = DayScene.SceneManager.Instance?.Character?.Character;
        if (player == null) return;

        var moveSpeedBonus = 0f;
        foreach (var (id, effect) in Effects)
            if (RunTimeAlbum.HasDecorationUsing(id))
                moveSpeedBonus += effect.DayMoveSpeedBonus;

        player.MoveSpeedMultiplier = RunTimePlayerData.LevelProfile.MoveSpeedMultiplier + moveSpeedBonus;
    }
}

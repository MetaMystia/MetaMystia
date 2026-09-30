using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

using Common;
using DEYU.AssetHandleUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;

using MetaMiku;
using MetaMystia.ResourceEx.Models;
using MetaMystia.ResourceEx.SpellCollection;
using MetaMystia.ResourceEx.Vfx;

using static MetaMystia.ResourceEx.AssetManagement.RexAssetRegistry;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 符卡注册器：按资源包 spells 声明创建对应的代码实现，并依游戏各数据库的初始化时机分步写入
/// 语言、角色标记、符卡实例与立绘。本类不含任何具体符卡的内容。
/// </summary>
[AutoLog]
public static partial class SpellRegistry
{
    /// <summary>implementation → 代码实现。新增符卡实现时在此加一行。</summary>
    private static readonly Dictionary<string, Func<int, VfxBundle, SpellBaseEx>> Implementations = new()
    {
        ["Mai"] = Create<Spell_Mai>,
        ["Minoriko"] = Create<Spell_Minoriko>,
    };

    /// <summary>宣言立绘的默认 pivot：宣言动画按 pivot 对位，取原版 104 张立绘的平均值。</summary>
    private static readonly Vector2 DefaultPortrayalPivot = new(0.497f, 0.644f);

    private sealed record PendingSpell(
        SpellBaseEx Spell,
        Il2CppReferenceArray<LanguageBase> Langs,
        Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>> Portrayal,
        SceneDirector.RuntimeHandle<SpellBase> Handle);

    private static readonly List<SpellConfig> SpellConfigs = [];
    private static readonly List<PendingSpell> _pending = [];

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.spells == null) return;

        foreach (var spellConfig in config.spells)
        {
            SpellConfigs.Add(spellConfig);
            Log.LogInfo($"[{packageName}] Loaded config for spell {spellConfig.id} ({spellConfig.implementation})");
        }
    }

    /// <summary>由 ResourceExManager 在 DataBaseCore 初始化后调用：创建实例并准备立绘与语言。</summary>
    internal static void InitializeAll()
    {
        foreach (var config in SpellConfigs)
            Initialize(config);
    }

    private static void Initialize(SpellConfig config)
    {
        if (!Implementations.TryGetValue(config.implementation ?? string.Empty, out var implementation))
        {
            Log.LogWarning($"符卡 {config.id} 的实现 {config.implementation} 不存在，跳过注册");
            return;
        }

        if (SpecialGuestRegistry.GetCharacterConfig(config.id) is not { } owner || string.IsNullOrWhiteSpace(owner.label))
        {
            Log.LogError($"符卡 {config.id} 找不到对应的角色，跳过注册");
            return;
        }

        if (string.IsNullOrWhiteSpace(config.positive?.name) || string.IsNullOrWhiteSpace(config.positive?.description) ||
            string.IsNullOrWhiteSpace(config.negative?.name) || string.IsNullOrWhiteSpace(config.negative?.description) ||
            string.IsNullOrWhiteSpace(config.positive?.portrait) || string.IsNullOrWhiteSpace(config.negative?.portrait))
        {
            Log.LogWarning($"符卡 {config.id} 缺少 positive/negative 的名称、说明或立绘路径，跳过注册");
            return;
        }

        VfxBundle vfx = null;
        if (!string.IsNullOrWhiteSpace(config.vfxBundle) && !AssetBundleRegistry.TryGet(config.vfxBundle, out vfx))
        {
            Log.LogWarning($"符卡 {config.id} 特效包未声明或加载失败: {config.vfxBundle}，跳过注册");
            return;
        }

        if (!TryGetSprite(config.positive.portrait, out var positive) ||
            !TryGetSprite(config.negative.portrait, out var negative))
        {
            Log.LogError($"符卡 {config.id} 立绘加载失败，跳过注册");
            return;
        }

        var spell = implementation(config.id, vfx);
        if (spell == null)
            return;
        spell.SpellId = config.id;
        spell.OwnerIdentifier = owner.label;
        spell.Vfx = vfx;

        var langs = new Il2CppReferenceArray<LanguageBase>(2);
        langs[0] = new LanguageBase(config.positive.name, config.positive.description);
        langs[1] = new LanguageBase(config.negative.name, config.negative.description);

        var pivot = config.portrayalPivot is [var x, var y] ? new Vector2(x, y) : DefaultPortrayalPivot;

        // il2cppinterop 把 ValueTuple 当带对象头的引用类型包装，直接 indexer 写入会把
        // 对象头当字段数据写进去。这里只构造，写入时使用 ForceAddOrUpdateValueTuple。
        _pending.Add(new PendingSpell(
            spell,
            langs,
            new Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>>(
                PortrayalHandle(positive, pivot), PortrayalHandle(negative, pivot)),
            new SceneDirector.RuntimeHandle<SpellBase>(spell)));
    }

    private static SpellBaseEx Create<T>(int id, VfxBundle vfx) where T : SpellBaseEx, ISpellDependencies
    {
        if (T.CheckDependencies(vfx) is { } reason)
        {
            Log.LogWarning($"符卡 {id} {reason}，跳过注册");
            return null;
        }

        // ClassInjector 必须先于 CreateInstance；同一实现可被多个角色复用，只注入一次。
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return ScriptableObject.CreateInstance<T>();
    }

    /// <summary>
    /// 用同一张贴图生成符卡宣言专用立绘，只改 pivot，不影响对话等界面共用的精灵图。
    /// </summary>
    private static IAssetHandle<Sprite> PortrayalHandle(Sprite source, Vector2 pivot)
    {
        var sprite = Sprite.Create(source.texture, source.rect, pivot, source.pixelsPerUnit);
        sprite.name = source.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return new SceneDirector.RuntimeHandle<Sprite>(sprite).Cast<IAssetHandle<Sprite>>();
    }

    internal static void RegisterAllLanguages()
    {
        foreach (var entry in _pending)
            DataBaseLanguage.SpellLang[entry.Spell.SpellId] = entry.Langs;
    }

    /// <summary>符卡字典与立绘：必须在 DataBaseNight.Initialize 之后写入，见 DataBaseNightPatch。</summary>
    internal static void RegisterAllInstances()
    {
        foreach (var entry in _pending)
        {
            DataBaseNight.SpecialGuestSpellPortrayal.ForceAddOrUpdateValueTuple(entry.Spell.SpellId, entry.Portrayal);
            DataBaseNight.SpecialGuestSpell[entry.Spell.SpellId] = entry.Handle.Cast<IAssetHandle<SpellBase>>();
        }
    }

    internal static void RegisterAllCharacterHasSpell()
    {
        foreach (var entry in _pending)
            DataBaseCharacter.CharacterHasSpell[entry.Spell.SpellId] = true;
    }
}

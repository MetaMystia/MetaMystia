using System;
using System.Collections.Generic;

using MetaMystia.ResourceEx.Models;
using MetaMystia.ResourceEx.SpellCollection;
using MetaMystia.ResourceEx.Vfx;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 符卡注册器：按资源包 <c>spells</c> 声明检查实现与特效包，并保管特效包供 <c>ISpell</c> 实现取用。
/// <para>符卡语言、宣言立绘、「是否拥有符卡」标记与符卡条目由框架按
/// <c>ModDatabaseExtension.OnInjectSpells</c> 写入；效果行为由模组实现 <c>Mystia.Spells.ISpell</c>，
/// 框架按 <c>SpellId</c> 匹配并在营业场景的服务作用域内执行其协程。</para>
/// 本类不含任何具体符卡的内容。
/// </summary>
[AutoLog]
public static partial class SpellRegistry
{
    /// <summary>implementation → 依赖检查。新增符卡实现时在此加一行。</summary>
    private static readonly Dictionary<string, Func<VfxBundle, string>> Implementations = new()
    {
        ["Mai"] = Spell_Mai.CheckDependencies,
    };

    private static readonly List<SpellConfig> SpellConfigs = [];

    /// <summary>spell id → 资源包声明的特效包；只有依赖齐全的声明才登记。</summary>
    private static readonly Dictionary<int, VfxBundle> VfxBundles = [];

    /// <summary>供 <c>ModDatabaseExtension</c> 映射数据面（语言与宣言立绘路径）。</summary>
    internal static IEnumerable<SpellConfig> Configs => SpellConfigs;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.spells == null) return;

        foreach (var spellConfig in config.spells)
        {
            SpellConfigs.Add(spellConfig);
            Log.LogInfo($"[{packageName}] Loaded config for spell {spellConfig.id} ({spellConfig.implementation})");
        }
    }

    /// <summary>由 ResourceExManager 在 DataBaseCore 初始化后调用：检查实现与特效包。</summary>
    internal static void InitializeAll()
    {
        foreach (var config in SpellConfigs)
            Initialize(config);
    }

    private static void Initialize(SpellConfig config)
    {
        if (!Implementations.TryGetValue(config.implementation ?? string.Empty, out var checkDependencies))
        {
            Log.LogWarning($"符卡 {config.id} 的实现 {config.implementation} 不存在，跳过注册");
            return;
        }

        VfxBundle vfx = null;
        if (!string.IsNullOrWhiteSpace(config.vfxBundle) && !AssetBundleRegistry.TryGet(config.vfxBundle, out vfx))
        {
            Log.LogWarning($"符卡 {config.id} 特效包未声明或加载失败: {config.vfxBundle}，跳过注册");
            return;
        }

        if (checkDependencies(vfx) is { } reason)
        {
            Log.LogWarning($"符卡 {config.id} {reason}，跳过注册");
            return;
        }

        VfxBundles[config.id] = vfx;
    }

    /// <summary>
    /// 实现与资源是否齐全。不齐全的符卡整张不注入：效果实现由框架按 <c>SpellId</c> 匹配，
    /// 数据面写入语言与立绘，二者缺一都会得到一张没有行为的符卡，与原实现跳过注册等价。
    /// </summary>
    internal static bool IsReady(SpellConfig config) => VfxBundles.ContainsKey(config.id);

    /// <summary><c>ISpell</c> 实现按 <c>SpellId</c> 取用资源包声明的特效包；未登记时为 null。</summary>
    internal static VfxBundle VfxFor(int spellId) => VfxBundles.GetValueOrDefault(spellId);
}

using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;

using GameData.Core.Collections.NightSceneUtility;

using MetaMystia.ResourceEx.Vfx;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 自定义符卡基类：只负责行为。名称、说明、立绘由资源包 spells 声明，buff 与特效 AssetBundle
/// 由资源包 buffs、assetBundles 声明；SpellRegistry 按 implementation 创建实例并赋予 ID 与归属角色。
///
/// 本类会被 ClassInjector 注入 il2cpp。注入抽象类时，Il2CppInterop 为本类声明的每个抽象方法
/// 追加虚表项，却没有为它们分配空间，越界写入进程堆，引发随机崩溃（PageHeap 已复现，
/// 见 docs/il2cppinterop-defects.md）。因此本类的抽象成员必须全部标注 [HideFromIl2Cpp]。
/// </summary>
public abstract class SpellBaseEx : SpellBase
{
    /// <summary>所属角色 ID，由 SpellRegistry 按配置赋值。</summary>
    [HideFromIl2Cpp] public int SpellId { get; internal set; }

    /// <summary>所属角色标识（characters 中的 label），由 SpellRegistry 按配置赋值。</summary>
    [HideFromIl2Cpp] public string OwnerIdentifier { get; internal set; }

    /// <summary>按配置关联的已加载特效包；未配置时为 null。</summary>
    [HideFromIl2Cpp] public VfxBundle Vfx { get; internal set; }

    public override string OnGettingSpellOwnerIdentifier() => OwnerIdentifier;

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
        => PositiveBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
        => NegativeBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    /// <summary>
    /// 游戏会等待两张卡的协程结束才继续符卡队列：协程只负责开场演出，
    /// buff 期间的持续效果与清理交给 buff 的结束回调。
    /// </summary>
    [HideFromIl2Cpp]
    protected abstract System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext);

    [HideFromIl2Cpp]
    protected abstract System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext);
}

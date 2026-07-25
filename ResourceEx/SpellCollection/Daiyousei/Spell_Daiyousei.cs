using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem.Collections;
using GameData.Core.Collections.NightSceneUtility;
using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia.ResourceEx.SpellCollection.Daiyousei;

/// <summary>
/// 大妖精符卡主类（U6a 注册阶段），仅完成类型注册与可被宣言所需的最小实现。
/// </summary>
[AutoLog]
public partial class Spell_Daiyousei : SpellBase
{
    /// <summary>
    /// 返回符卡归属角色标识，供宣言日志与立绘偏移识别使用。
    /// 标识统一取自 SpellHelper.DaiyouseiOwnerIdentifier，保证与立绘偏移表键一致。
    /// </summary>
    /// <returns>归属角色标识字符串</returns>
    public override string OnGettingSpellOwnerIdentifier()
    {
        return SpellHelper.DaiyouseiOwnerIdentifier;
    }

    /// <summary>
    /// 宣言演出即将播放时被原生流程调用一次：写入立绘偏移 pending flag（由 SpellDeclareCutinCharacterPatch 在立绘 OnEnable 时消费），并返回 true 允许自动宣言。
    /// </summary>
    /// <param name="isPositiveSpell">本次宣言是否为红卡（true）/黑卡（false）</param>
    /// <returns>是否允许游戏自动播放符卡宣言演出</returns>
    public override bool ShouldCallSpellDeclarationAuto(bool isPositiveSpell)
    {
        SpellHelper.SetCutinShift(SpellHelper.DaiyouseiOwnerIdentifier);
        return true;
    }

    /// <summary>
    /// 红卡效果入口。本阶段符卡仅完成注册可被宣言，尚未实现红卡效果，返回 null 使流程不触发任何效果。
    /// </summary>
    /// <param name="spellExecutionContext">符卡执行上下文，提供角色与回调等信息</param>
    /// <returns>il2cpp 协程迭代器；本阶段返回 null 表示不触发任何效果</returns>
    public override IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
    {
        return null;
    }

    /// <summary>
    /// 黑卡效果入口。本阶段符卡仅完成注册可被宣言，尚未实现黑卡效果，返回 null 使流程不触发任何效果。
    /// </summary>
    /// <param name="spellExecutionContext">符卡执行上下文，提供角色与回调等信息</param>
    /// <returns>il2cpp 协程迭代器；本阶段返回 null 表示不触发任何效果</returns>
    public override IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
    {
        return null;
    }
}

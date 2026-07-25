using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem.Collections;
using GameData.Core.Collections.NightSceneUtility;
using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.SpellCollection.Daiyousei;

/// <summary>
/// 大妖精符卡主类（U6a 注册阶段），仅完成类型注册与可被宣言所需的最小实现。
/// </summary>
[AutoLog]
public partial class Spell_Daiyousei : SpellBase
{
    // 符卡归属角色标识，与 SpellHelper.CutinShift 表键一致（立绘偏移待 U6b 接入）。
    private const string DaiyouseiOwnerIdentifier = "_ResourceExample_Daiyousei";

    /// <summary>
    /// 返回符卡归属角色标识，供宣言日志与立绘偏移识别使用。
    /// </summary>
    /// <returns>归属角色标识字符串</returns>
    public override string OnGettingSpellOwnerIdentifier()
    {
        return DaiyouseiOwnerIdentifier;
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

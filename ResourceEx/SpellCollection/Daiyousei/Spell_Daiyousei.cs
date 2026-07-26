using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem.Collections;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage.Collections;
using MetaMystia.ResourceEx.SpellCollection;
using MetaMystia.UI;
using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.SpellCollection.Daiyousei;

/// <summary>
/// 大妖精符卡主类：红卡「妖精的呼朋引伴」召唤稀客，黑卡「飞雾」注册定时 Buff。
/// </summary>
[AutoLog]
public partial class Spell_Daiyousei : SpellBase
{
    internal const int DaiyouseiFogBuffType = 100;

    // 黑卡「飞雾」Buff 持续秒数。
    private const int DaiyouseiFogDurationSeconds = 30;

    // 红卡召唤池「妖精三人组」：莉格露(0)、露米娅(1)、琪露诺(28)
    private static readonly int[] FairyFriendIds = { 0, 1, 28 };
    // 红卡降级召唤：上白泽慧音(4)
    private const int KeineGuestId = 4;
    // 全员在场时发放的水果个数
    private const int FruitGrantCount = 3;

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
    /// 宣言演出即将播放时被原生流程调用一次
    /// </summary>
    /// <param name="isPositiveSpell">本次宣言是否为红卡（true）/黑卡（false）</param>
    /// <returns>是否允许游戏自动播放符卡宣言演出</returns>
    public override bool ShouldCallSpellDeclarationAuto(bool isPositiveSpell)
    {
        SpellHelper.SetCutinShift(SpellHelper.DaiyouseiOwnerIdentifier);
        return true;
    }

    /// <summary>
    /// 红卡「妖精的呼朋引伴」效果入口：优先召唤未在场的妖精三人组成员，三人都在场则召唤慧音，四人都在场则发放水果。
    /// </summary>
    /// <param name="spellExecutionContext">符卡执行上下文，提供角色与回调等信息</param>
    /// <returns>il2cpp 协程迭代器，驱动召唤或水果发放动画</returns>
    public override IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
    {
        return PositiveBuffRoutine().WrapToIl2Cpp();
    }

    /// <summary>
    /// 红卡三层判定：未在场妖精随机召唤 → 慧音降级召唤 → 水果发放兜底。
    /// </summary>
    /// <returns>托管协程迭代器</returns>
    [HideFromIl2Cpp]
    private System.Collections.IEnumerator PositiveBuffRoutine()
    {
        var onFieldSpecialIds = SpellHelper.GetOnFieldSpecialGuestIds();

        var absentFriendIds = new List<int>();
        foreach (var friendId in FairyFriendIds)
        {
            if (!onFieldSpecialIds.Contains(friendId)) absentFriendIds.Add(friendId);
        }

        if (absentFriendIds.Count > 0)
        {
            var chosenFriendId = absentFriendIds[UnityEngine.Random.Range(0, absentFriendIds.Count)];
            SummonGuestWithNotice(chosenFriendId);
            yield break;
        }

        if (!onFieldSpecialIds.Contains(KeineGuestId))
        {
            SummonGuestWithNotice(KeineGuestId);
            yield break;
        }

        yield return DaiyouseiFruitEffect.GrantFruitsRoutine(FruitGrantCount);
    }

    /// <summary>
    /// 召唤指定稀客，成功后弹出顶部提示文案（慧音使用专属文案）。
    /// </summary>
    /// <param name="guestId">目标稀客 id</param>
    [HideFromIl2Cpp]
    private static void SummonGuestWithNotice(int guestId)
    {
        if (!SpellHelper.TrySummonSpecialGuest(guestId)) return;

        var noticeMessage = guestId == KeineGuestId
            ? TextId.Spell_Daiyousei_SummonKeine.Get()
            : TextId.Spell_Daiyousei_SummonFriend.Get(guestId.GetSpecialGuestLang().BriefName);
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage(noticeMessage);
    }

    /// <summary>
    /// 黑卡 雾符「妖精的薄雾」效果入口：同步注册 30 秒飞雾 Buff，并解耦启动屏幕空间雾气视觉。
    /// </summary>
    /// <param name="spellExecutionContext">符卡执行上下文，提供角色与回调等信息</param>
    /// <returns>il2cpp 协程迭代器；返回 null 表示无额外同步视觉效果，雾气视觉经独立协程异步播放</returns>
    public override IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
    {
        SpellHelper.RegisterTimedBuff(
            Manager,
            DaiyouseiFogDurationSeconds,
            (EventManager.BuffType)DaiyouseiFogBuffType,
            out _,
            null);
        StartFogVisual();
        return null;
    }

    /// <summary>
    /// 启动黑卡雾气视觉协程，与符卡执行解耦以免阻塞符卡收尾（30 秒寿命自管理）。
    /// 若 PluginManager 未就绪则跳过视觉、仅保留右下角 Buff 栏条目。
    /// </summary>
    [HideFromIl2Cpp]
    private static void StartFogVisual()
    {
        if (PluginManager.Instance == null)
        {
            Log.LogWarning("[Daiyousei] PluginManager 未就绪，跳过雾气视觉");
            return;
        }
        PluginManager.Instance.StartManagedCoroutine(DaiyouseiFogEffect.StartFogRoutine(DaiyouseiFogDurationSeconds));
    }
}

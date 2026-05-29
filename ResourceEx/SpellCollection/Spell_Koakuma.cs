using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 小恶魔 Spell_Koakuma — 红卡：接下来 3 次稀客点单时提示订单内容。
///
/// 原理：
///   RedCard → RegisterCountedBuff(Koakuma, 3)
///   稀客点单 → Patch 触发 OnSpecialGuestOrder() → 显示通知 → Deduct
/// </summary>
[AutoLog]
public partial class Spell_Koakuma : SpellBase
{
    private const int MAX_CHARGES = 3;

    public override string OnGettingSpellOwnerIdentifier() => "Koakuma";

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext ctx)
        => PositiveBuffRoutine(ctx).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext ctx)
        => NegativeBuffRoutine(ctx).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        // 注册 Buff 描述（$a 被游戏替换为剩余次数）
        var buffType = (EventManager.BuffType)9001;
        var desc = new GameData.CoreLanguage.ObjectLanguageBase(
            "灵符「遗失典籍的回响」",
            "小恶魔从图书馆搬来一本百科全书，接下来 $a 次稀客点单会提示具体内容",
            EventManager.BuffType.PhilosopherStone.RefBuffLang().Visual);
        DataBaseLanguage.BuffDescription[buffType] = desc;

        EventManager.Instance.RegisterCountedBuff(buffType, MAX_CHARGES, EventManager.MathOperation.Add, null, null);
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"小恶魔从图书馆搬来一本百科全书，接下来 {MAX_CHARGES} 次稀客点单会提示具体内容！");
        Log.Info($"[Spell_Koakuma] 注册 CountedBuff ×{MAX_CHARGES}");
        yield return null;
    }

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("小恶魔的黑卡……好像什么也没发生");
        yield return null;
    }

    // ===================================================================
    //  静态方法 — 供 Harmony Patch 调用
    // ===================================================================

    public static bool HasBuff()
        => EventManager.Instance.CheckCountedBuffExists((EventManager.BuffType)9001);

    private static void Deduct()
        => EventManager.Instance.TryDeductCountedBuffValue((EventManager.BuffType)9001);

    /// <summary>
    /// 由 GuestsManager.GenerateOrderSession 的 Postfix 触发。
    /// 检查 Koakuma CountedBuff 是否存在，存在则显示订单详情并消耗一层。
    ///
    /// ⚠️ 稀客的 foodRequest/beverageRequest 是标签 ID（tag），不是具体物品 ID！
    ///    用 GetFoodTag() / GetBeverageTag() 转成标签名，别去查 DataBaseCore.Foods。
    /// </summary>
    public static void OnSpecialGuestOrder(GuestGroupController guestGroup)
    {
        if (!HasBuff()) return;

        var order = guestGroup.PeekOrders();
        if (order == null) return;

        var foodTag = order.foodRequest.GetFoodTag();
        var bevTag = order.beverageRequest.GetBeverageTag();
        var guestName = guestGroup.OnGetGuestName();

        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"{guestName} 想要「{foodTag}」标签的料理 + 「{bevTag}」标签的酒水");
        Log.Info($"[Spell_Koakuma] {guestName}: foodTag={foodTag} bevTag={bevTag}");
        Deduct();
    }
}

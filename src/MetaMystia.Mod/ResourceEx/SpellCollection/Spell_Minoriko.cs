using System.Collections;
using System.Linq;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections;
using GameData.Core.Collections.NightSceneUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI;

using MetaMystia.Multiplayer;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.ResourceEx.Vfx;
using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>秋穰子的丰穣结界与饱腹惩罚。显示数据和特效由资源包提供。</summary>
public sealed class Spell_Minoriko : SpellBaseEx, ISpellDependencies
{
    internal const EventManager.BuffType HarvestBuff = (EventManager.BuffType)10001;
    private const int FillingTag = 9;
    private const int Duration = 20;

    [HideFromIl2Cpp]
    public static string CheckDependencies(VfxBundle vfx)
    {
        if (vfx == null)
            return "缺少 vfxBundle";
        foreach (var name in new[] { "Minoriko_Cast", "Minoriko_Harvest", "Minoriko_Autumn", "Minoriko_Sated" })
            if (!vfx.Contains(name))
                return $"特效包缺少预制件 {name}";
        return BuffRegistry.IsAvailable((int)HarvestBuff) ? null : "丰穣结界 buff 未声明或资源缺失";
    }

    [HideFromIl2Cpp]
    protected override IEnumerator PositiveBuffRoutine(SpellExecutionContext context)
    {
        var origin = context.GuestPosition.HasValue ? context.GuestPosition.Value : GetPlayerPosition();
        Vfx.PlayOneShot("Minoriko_Cast", origin);
        yield return new WaitForSeconds(1.2f);

        // 重复触发由原版延长计时，不重复添加 tag、修正器或持续特效。
        RegisterTimedBuff(HarvestBuff, Duration,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>((int duration) => RegisterHarvest(duration, origin)), extraDuration: 0);
    }

    [HideFromIl2Cpp]
    private void RegisterHarvest(int duration, Vector3 origin)
    {
        var manager = Manager;
        var harvest = Vfx.Play("Minoriko_Harvest", origin);
        // 与梅蒂欣相同：玩家和伙伴的 MatchedCookCombo.GetResult 将 tag 写入新料理。
        manager.SetExtraCookTag(FillingTag);
        manager.CookTimeAndOrderRateEditByTag(
            FillingTag, 0f, 0f, duration, out _,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
            {
                manager.RemoveExtraCookTag(FillingTag);
                MinorikoSake.SetActive(false);
                Vfx.Stop(harvest);
            }),
            DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int, string, string>>(
                (int time, string description) => description.Replace("$a", time.ToString())),
            HarvestBuff,
            // 不读取已附加结界 tag 的 Tags，防止全部料理都被判定为原本饱腹。
            DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Sellable, float, bool>>(
                (Sellable food, float _) => food.tags.Contains(FillingTag)));
        MinorikoSake.SetActive(true);
    }

    [HideFromIl2Cpp]
    protected override IEnumerator NegativeBuffRoutine(SpellExecutionContext context)
    {
        var origin = GetThrowOrigin(context);
        if (Manager.CheckTimedBuffExists(HarvestBuff))
            Manager.RemoveAllRegisteredTimedBuff(HarvestBuff, false);
        Vfx.PlayOneShot("Minoriko_Autumn", origin);

        var guests = new Il2CppSystem.Collections.Generic.List<GuestGroupController>(
            GuestsManager.Instance.AllPresentedGuestGroupController.Cast<Il2CppSystem.Collections.Generic.IEnumerable<GuestGroupController>>()).ToManagedList();
        var foods = new Il2CppSystem.Collections.Generic.List<int>(DataBaseCore.GetAllFoods()).ToManagedList();
        var animationOnly = !GameFlow.ShouldSkipAction && GameSession.HasRoomPeers && GameSession.IsRoomClient;
        foreach (var guest in guests)
        {
            // 每组共用点单额度，只扣一次；不取消已在处理的订单，也不减为负数。
            if (!animationOnly && guest.RemainOrderCount > 0)
                guest.AddExtraOrderCount(-1, false);

            foreach (var character in guest.guestInstances)
            {
                if (character == null || foods.Count == 0)
                    continue;
                var food = DataBaseCore.RefFood(foods[Random.Range(0, foods.Count)]);
                Manager.StartCoroutine(FeedVisual(food.Text.Visual, character.transform, origin).WrapToIl2Cpp());
            }
        }
        yield return new WaitForSeconds(1.2f);
    }

    [HideFromIl2Cpp]
    private Vector3 GetThrowOrigin(SpellExecutionContext context)
    {
        // GuestPosition 在不记录符卡次数时返回玩家位置，不能据此判断触发角色是否在场。
        if (context.GuestCharacterInstances != null)
            foreach (var character in context.GuestCharacterInstances)
                if (character != null)
                    return character.transform.position;

        var guests = new Il2CppSystem.Collections.Generic.List<GuestGroupController>(
            GuestsManager.Instance.AllPresentedGuestGroupController.Cast<Il2CppSystem.Collections.Generic.IEnumerable<GuestGroupController>>()).ToManagedList();
        foreach (var guest in guests)
        {
            var specialGuest = guest.TryCast<SpecialGuestsController>();
            if (specialGuest?.SpecialGuest?.StringId != OwnerIdentifier)
                continue;
            foreach (var character in specialGuest.guestInstances)
                if (character != null)
                    return character.transform.position;
        }

        // 无实际触发角色且所属角色不在场时，沿用原版代触发的玩家位置。
        return GetPlayerPosition();
    }

    [HideFromIl2Cpp]
    private IEnumerator FeedVisual(Sprite food, Transform guest, Vector3 origin)
    {
        if (guest == null)
            yield break;
        // 仅借用原版投掷演出，不写入顾客订单或结算料理。
        yield return UIManager.Instance.ExecuteThrowDeliver(food, guest.position, origin);
        if (guest != null)
            Vfx.PlayOneShot("Minoriko_Sated", guest.position);
    }
}

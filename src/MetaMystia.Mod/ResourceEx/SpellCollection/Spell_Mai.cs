using System;
using System.Collections;
using System.Collections.Generic;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using NightScene.PartnerUtility;
using NightScene.Tiles;
using NightScene.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Patch;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.ResourceEx.Vfx;
using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 舞（11001）的符卡实现，名称、说明、立绘、buff 与特效资源见资源包 ResourceExample。
///
/// 红卡：30 秒内持续用冰系魔法调酒，为在场普通订单送上其点单的酒水。
/// 黑卡：30 秒内所有客人的料理必须带有「凉爽」tag，否则评价上限为「普通」。
/// </summary>
public sealed class Spell_Mai : SpellBaseEx, ISpellDependencies
{
    // 资源包 buffs 中声明的 buff。
    private const EventManager.BuffType RewardBuff = (EventManager.BuffType)11002;
    private const EventManager.BuffType PunishmentBuff = (EventManager.BuffType)11003;

    private const int BuffSeconds = 30;
    private const float ServeIntervalSeconds = 1f;
    private const int CoolTag = 21;
    private const int EvalNormal = 2;

    private const string CastVfx = "Mai_Cast";
    private const string IceShardVfx = "Mai_IceShard";
    private const string SnowfallVfx = "Mai_Snowfall";
    private const string BevTrailVfx = "Mai_BevTrail";
    private const string FrostFieldVfx = "Mai_FrostField";
    private const string CoolDownVfx = "Mai_CoolDown";

    /// <summary>在类型注入与实例创建前检查必要资源；返回 null 表示齐全。</summary>
    [HideFromIl2Cpp]
    public static string CheckDependencies(VfxBundle vfx)
    {
        if (vfx == null)
            return "缺少 vfxBundle";
        foreach (var prefab in new[] { CastVfx, IceShardVfx, SnowfallVfx, BevTrailVfx, FrostFieldVfx, CoolDownVfx })
            if (!vfx.Contains(prefab))
                return $"特效包缺少预制件 {prefab}";
        foreach (var buff in new[] { RewardBuff, PunishmentBuff })
            if (!BuffRegistry.IsAvailable((int)buff))
                return $"buff {(int)buff} 未声明或名称、说明、图标缺失";
        return null;
    }

    /// <summary>正在跑的上酒协程；重复触发或 buff 结束时停掉。</summary>
    private Coroutine _serveLoop;

    /// <summary>客机记录本轮已播放过投掷动画的订单（按原生指针），避免同一订单每秒重复投掷。</summary>
    private HashSet<IntPtr> _animatedOrders;

    /// <summary>
    /// 联机客机只播放动画、不改订单：上酒由主机的符卡实际生效，订单补齐后主机结算，
    /// 经 EvaluateOrderMessage 把酒水与结果同步给客机。
    /// </summary>
    private static bool AnimationOnly =>
        !GameFlow.ShouldSkipAction && GameSession.HasRoomPeers && GameSession.IsRoomClient;

    #region 红卡

    [HideFromIl2Cpp]
    protected override IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        var origin = spellExecutionContext.GuestPosition.HasValue
            ? spellExecutionContext.GuestPosition.Value
            : GetPlayerPosition();
        var cast = Vfx.PlayOneShot(CastVfx, origin);
        yield return new WaitForSeconds(2f);
        Vfx.Stop(cast);

        // buff 已存在时 TryOverrideTimedBuff 只延长时长、不调用回调，持续效果须在回调内创建。
        RegisterTimedBuff(
            RewardBuff,
            BuffSeconds,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(OnBuffRegistered),
            extraDuration: 0);

        void OnBuffRegistered(int duration)
        {
            StopServeLoop();
            _animatedOrders = [];
            var snowfall = Vfx.Play(SnowfallVfx);

            Manager.RegisterTimedBuff(
                duration,
                RewardBuff,
                out _,
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
                {
                    Vfx.Stop(snowfall);
                    StopServeLoop();
                }),
                DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int, string, string>>(
                    (int currentTime, string description) => description.Replace("$c", currentTime.ToString())));

            // 挂在夜间场景的 EventManager 上，离开场景时随之停止；buff 结束时由回调停止。
            _serveLoop = Manager.StartCoroutine(ServeLoop().WrapToIl2Cpp());
        }
    }

    [HideFromIl2Cpp]
    private void StopServeLoop()
    {
        if (_serveLoop != null)
            Manager.StopCoroutine(_serveLoop);
        _serveLoop = null;
    }

    /// <summary>每秒为一桌送上酒水。一次只处理一桌，避免全场投掷动画同时重叠。</summary>
    [HideFromIl2Cpp]
    private IEnumerator ServeLoop()
    {
        var wait = new WaitForSeconds(ServeIntervalSeconds);
        while (true)
        {
            yield return wait;
            TryServeOneOrder();
        }
    }

    [HideFromIl2Cpp]
    private void TryServeOneOrder()
    {
        // AllGuestInDeskController 是 Il2Cpp 字典的 Values，先转成托管列表再遍历。
        var guests = new Il2CppSystem.Collections.Generic.List<GuestGroupController>(
            GuestsManager.Instance.AllGuestInDeskController);

        foreach (var guest in guests.ToManagedList())
        {
            if (guest is null || guest.AllOrdersCount <= 0)
                continue;

            var order = guest.PeekOrders();
            if (order is null || order.ServBeverage != null || order.ServedBeverageInAir != null)
                continue;

            // 玩家正在这桌的上菜面板里：面板提交时不复查订单，抢先上酒会被覆盖并重复结算。
            if (WorkSceneServePannelPatch.PanelDeskCode == order.DeskCode)
                continue;

            // 只处理普通订单（NormalOrder），按订单而非客人类型判断。
            var beverage = order.TryCast<GuestsManager.NormalOrder>()?.RequestBeverage;
            if (beverage is null)
                continue;

            if (AnimationOnly && !_animatedOrders.Add(order.Pointer))
                continue;

            Serve(guest, order, beverage);
            return;
        }
    }

    /// <summary>
    /// 复用游戏原本的上酒流程：先登记在空中并通知伙伴，播放投掷动画，
    /// 落地后复查订单，再写入 ServBeverage，订单齐备时结算。客机只播放动画。
    /// </summary>
    [HideFromIl2Cpp]
    private void Serve(GuestGroupController guest, GuestsManager.OrderBase order, Sellable beverage)
    {
        var origin = GetPlayerPosition();
        var target = GetGuestTable(order.DeskCode);
        var visual = beverage.Text?.Visual;
        var animationOnly = AnimationOnly;

        if (!animationOnly)
        {
            order.ServedBeverageInAir = beverage;
            // 与原版玩家上菜一致，发射时即通知：正端着酒赶往这桌的伙伴会就此中断。
            PartnerManager.Instance.OnOrderBaseStatusUpdate(
                order, PartnerManager.OrderChangeContext.BeverageDelivered, -1);
        }
        Manager.StartCoroutine(ThrowThenServe().WrapToIl2Cpp());

        IEnumerator ThrowThenServe()
        {
            var trail = Vfx.Play(BevTrailVfx, origin);
            // 游戏方法本身返回 Il2Cpp 的 IEnumerator，直接 yield 交给 Unity 推进。
            if (visual != null)
                yield return UIManager.Instance.ExecuteThrowDeliver(visual, target, origin);

            Vfx.Stop(trail);
            Vfx.PlayOneShot(IceShardVfx, target);

            if (animationOnly || !IsStillInAir())
                yield break;

            order.ServBeverage = beverage;
            order.ServedBeverageInAir = null;
            TileManager.Instance.GuestTables[order.DeskCode].tableDisplayer.SetBeverageVisual(visual);

            if (order.IsFullfilled)
                GuestsManager.Instance.EvaluateOrder(guest, true, null);
        }

        // 飞行期间客人可能离开、桌位换人，或空中酒水被其他投掷覆盖；任一情况都放弃这杯。
        bool IsStillInAir() =>
            GuestsManager.Instance.GetInDeskGuest(order.DeskCode)?.Pointer == guest.Pointer
            && guest.AllOrdersCount > 0
            && guest.PeekOrders()?.Pointer == order.Pointer
            && order.ServedBeverageInAir?.Pointer == beverage.Pointer;
    }

    #endregion

    #region 黑卡

    [HideFromIl2Cpp]
    protected override IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        var frost = Vfx.PlayScreenOverlay(FrostFieldVfx);
        var coolDown = Vfx.Play(CoolDownVfx, GetPlayerPosition());
        EventCoroutineDelegation.Schedule(SetCameraShake(0.35f, 0.35f, 0.4f));

        // 不含「凉爽」tag 的料理，评价上限压到「普通」；containsOrNot=false 表示缺少该 tag 时生效。
        var coolTags = new Il2CppSystem.Collections.Generic.List<int>();
        coolTags.Add(CoolTag);

        // 与原版 Spell_Kagerou 一致：协程只负责演出，buff 结束后的清理交给 onBuffEnd。
        Manager.MaxEvalLevelSet(
            BuffSeconds,
            EvalNormal,
            coolTags.ToIEnumerable(),
            out _,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
            {
                Vfx.Stop(frost);
                Vfx.Stop(coolDown);
            }),
            PunishmentBuff,
            isFood: true,
            overrideDescription: DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int, string, string>>(
                (int currentTime, string description) => description
                    .Replace("$a", DataBaseLanguage.GetFoodTag(CoolTag))
                    .Replace("$b", DataBaseLanguage.GetEvalText(EvalNormal))
                    .Replace("$c", currentTime.ToString())),
            containsOrNot: false);

        yield break;
    }

    #endregion
}

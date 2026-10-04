using System;
using System.Collections;
using System.Collections.Generic;

using Il2CppInterop.Runtime;
using UnityEngine;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;
using NightScene.PartnerUtility;
using NightScene.Tiles;
using NightScene.UI;

using Mystia;
using Mystia.Scenes;
using Mystia.Spells;

using MetaMystia.Listeners;
using MetaMystia.Multiplayer;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.ResourceEx.Vfx;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 舞（11001）的符卡实现，名称、说明、立绘、buff 与特效资源见资源包 ResourceExample。
///
/// 红卡：30 秒内持续用冰系魔法调酒，为在场普通订单送上其点单的酒水。
/// 黑卡：30 秒内所有客人的料理必须带有「凉爽」tag，否则评价上限为「普通」。
///
/// 实例由框架按 <c>SpellData.Id</c> 匹配，效果在营业场景服务作用域内以托管协程执行；
/// 特效包由 <see cref="SpellRegistry"/> 按资源包声明取出，例行日志走框架传入的 <see cref="ILog"/>。
/// </summary>
public sealed class Spell_Mai : ISpell, ISpellDependencies
{
    /// <summary>符卡 id：资源包 <c>spells[].id</c>，与所属角色（舞）的 id 一致。</summary>
    private const int Spell = 11001;

    // 资源包 buffs 中声明的 buff。
    private const int RewardBuff = 11002;
    private const int PunishmentBuff = 11003;

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

    /// <summary>在实例生效前检查必要资源；返回 null 表示齐全。</summary>
    public static string CheckDependencies(VfxBundle vfx)
    {
        if (vfx == null)
            return "缺少 vfxBundle";
        foreach (var prefab in new[] { CastVfx, IceShardVfx, SnowfallVfx, BevTrailVfx, FrostFieldVfx, CoolDownVfx })
            if (!vfx.Contains(prefab))
                return $"特效包缺少预制件 {prefab}";
        foreach (var buff in new[] { RewardBuff, PunishmentBuff })
            if (!BuffRegistry.IsAvailable(buff))
                return $"buff {buff} 未声明或名称、说明、图标缺失";
        return null;
    }

    public int SpellId => Spell;

    /// <summary>资源包声明的特效包；<see cref="CheckDependencies"/> 通过时必定可用。</summary>
    private static VfxBundle Vfx => SpellRegistry.VfxFor(Spell);

    /// <summary>正在跑的上酒协程；重复触发或 buff 结束时停掉。</summary>
    private CoroutineHandle _serveLoop;

    /// <summary>客机记录本轮已播放过投掷动画的订单（按原生指针），避免同一订单每秒重复投掷。</summary>
    private HashSet<OrderHandle> _animatedOrders;

    /// <summary>
    /// 联机客机只播放动画、不改订单：上酒由主机的符卡实际生效，订单补齐后主机结算，
    /// 经 EvaluateOrderMessage 把酒水与结果同步给客机。
    /// </summary>
    private static bool AnimationOnly =>
        !GameFlow.ShouldSkipAction && GameSession.HasRoomPeers && GameSession.IsRoomClient;

    #region 红卡

    public IEnumerator? Positive(IWorkSceneServices scene, ICoroutineDispatcher coroutines, ILog log) =>
        PositiveRoutine(scene, coroutines, log);

    private IEnumerator PositiveRoutine(IWorkSceneServices scene, ICoroutineDispatcher coroutines, ILog log)
    {
        var origin = GuestPosition(scene) ?? Unity(scene.Presentation.PlayerPosition);
        var cast = Vfx.PlayOneShot(CastVfx, origin);
        yield return coroutines.AfterSeconds(2f);
        Vfx.Stop(cast);

        // buff 已存在时游戏只延长时长、不重跑已注册的持续效果，持续效果因此挂在本次注册的结束回调之后。
        GameObject snowfall = null;
        void OnBuffEnd()
        {
            Vfx.Stop(snowfall);
            StopServeLoop(coroutines);
        }

        scene.Buffs.RegisterTimedBuff(RewardBuff, BuffSeconds, OnBuffEnd, BuffDescription, isPositive: true);
        if (!scene.Buffs.HasTimedBuff(RewardBuff))
        {
            // buff 被禁止（NoBuffTime）时游戏立即触发结束回调，此时不建立持续效果。
            log.Warning($"符卡 {Spell} 的奖励 buff 未注册，跳过持续演出");
            yield break;
        }

        StopServeLoop(coroutines);
        _animatedOrders = [];
        snowfall = Vfx.Play(SnowfallVfx);

        // 挂在场景协程宿主上，离开场景时随之停止；buff 结束时由回调停止。
        _serveLoop = coroutines.StartOn(coroutines.Owner, _ => ServeLoop(scene, coroutines));
    }

    /// <summary>buff 剩余秒数的 $c 占位符替换（原 <c>SpellBase.RegisterTimedBuff</c> 的默认处理）。</summary>
    private static string BuffDescription(int currentTime, string description) =>
        description.Replace("$c", currentTime.ToString());

    /// <summary>符卡目标（指定稀客）的世界坐标；虚客或找不到目标时返回 null，调用方回退到玩家位置。</summary>
    private static Vector3? GuestPosition(IWorkSceneServices scene)
    {
        var guestId = scene.Spells.GuestId;
        if (guestId < 0)
            return null;

        foreach (var group in scene.Guests.InDeskGuests)
        {
            if (!group.TryGet(out var guest) || guest.Kind != GuestKind.Special || guest.GuestIds[0] != guestId)
                continue;

            if (scene.Presentation.TryGetGuestPosition(group, out var position))
                return Unity(position);
        }
        return null;
    }

    /// <summary>框架的镜像向量转成 Unity 向量：模组的特效层（VfxBundle）仍直接操作 Unity 对象。</summary>
    private static Vector3 Unity(Mystia.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);

    private void StopServeLoop(ICoroutineDispatcher coroutines)
    {
        if (_serveLoop != default)
            coroutines.Stop(_serveLoop);
        _serveLoop = default;
    }

    /// <summary>每秒为一桌送上酒水。一次只处理一桌，避免全场投掷动画同时重叠。</summary>
    private IEnumerator ServeLoop(IWorkSceneServices scene, ICoroutineDispatcher coroutines)
    {
        var wait = new WaitForSeconds(ServeIntervalSeconds);
        while (true)
        {
            yield return wait;
            TryServeOneOrder(scene, coroutines);
        }
    }

    private void TryServeOneOrder(IWorkSceneServices scene, ICoroutineDispatcher coroutines)
    {
        foreach (var guest in scene.Guests.InDeskGuests)
        {
            if (!scene.Guests.TryGetPendingOrder(guest, out var order))
                continue;
            if (order.Beverage != null || order.BeverageInAir != null)
                continue;

            // 玩家正在这桌的上菜面板里：面板提交时不复查订单，抢先上酒会被覆盖并重复结算。
            if (WorkSync.ServePanel?.DeskCode == order.DeskCode)
                continue;

            // 只处理普通订单（按订单类型判断）；点单里的酒水按 id 现取一份（原来取订单的 RequestBeverage）。
            if (order.Kind != OrderKind.Normal || order.BeverageRequest < 0)
                continue;

            if (AnimationOnly && !_animatedOrders.Add(order.Handle))
                continue;

            Serve(scene, coroutines, guest, order, order.BeverageRequest.AsNewBeverage());
            return;
        }
    }

    /// <summary>
    /// 复用游戏原本的上酒流程：先登记在空中并通知伙伴，播放投掷动画，
    /// 落地后复查订单，再写入酒水槽位，订单齐备时结算。客机只播放动画。
    /// </summary>
    private void Serve(
        IWorkSceneServices scene,
        ICoroutineDispatcher coroutines,
        GuestHandle guest,
        OrderProxy order,
        Sellable beverage)
    {
        var origin = Unity(scene.Presentation.PlayerPosition);
        var target = Unity(scene.Presentation.TablePosition(order.DeskCode));
        var visual = beverage.Text?.Visual;
        var animationOnly = AnimationOnly;
        var dish = scene.Dishes.DishOf(beverage);

        if (!animationOnly)
        {
            scene.Guests.SetBeverageInAir(guest, dish);
            // 与原版玩家上菜一致，发射时即通知：正端着酒赶往这桌的伙伴会就此中断。
            scene.Guests.NotifyOrderStatusUpdate(order.Handle, PartnerOrderContext.BeverageDelivered, -1);
        }
        coroutines.StartOn(coroutines.Owner, _ => ThrowThenServe());

        IEnumerator ThrowThenServe()
        {
            var trail = Vfx.Play(BevTrailVfx, origin);
            // 游戏方法本身返回 Il2Cpp 的 IEnumerator，直接交给协程泵推进。
            if (visual != null)
                yield return UIManager.Instance.ExecuteThrowDeliver(visual, target, origin);

            Vfx.Stop(trail);
            Vfx.PlayOneShot(IceShardVfx, target);

            if (animationOnly || !IsStillInAir()) yield break;

            order.SetBeverage(dish);
            order.SetBeverageInAir(null);
            scene.Guests.ShowServedDish(order.DeskCode, dish, DishKind.Beverage);

            if (order.IsFulfilled)
                scene.Guests.Evaluate(guest);
        }

        // 飞行期间客人可能离开、桌位换人，或空中酒水被其他投掷覆盖；任一情况都放弃这杯。
        bool IsStillInAir() =>
            scene.Guests.TryGetSeated(order.DeskCode, out var seated) && seated.Handle == guest
            && scene.Guests.TryGetPendingOrder(guest, out var pending) && pending.Handle == order.Handle
            && order.BeverageInAir?.Handle == dish?.Handle;
    }

    #endregion

    #region 黑卡

    public IEnumerator? Negative(IWorkSceneServices scene, ICoroutineDispatcher coroutines, ILog log) =>
        NegativeRoutine(scene, coroutines, log);

    private IEnumerator NegativeRoutine(IWorkSceneServices scene, ICoroutineDispatcher coroutines, ILog log)
    {
        // 已生效时只追加剩余时间，保留原有评价限制、特效与结束回调。
        if (scene.Buffs.HasTimedBuff(PunishmentBuff))
        {
            scene.Buffs.ExtendTimedBuff(PunishmentBuff, BuffSeconds);
            yield break;
        }

        var frost = Vfx.PlayScreenOverlay(FrostFieldVfx);
        var coolDown = Vfx.Play(CoolDownVfx, Unity(scene.Presentation.PlayerPosition));
        coroutines.StartOn(coroutines.Owner, _ => Shake());

        // 与原版 Spell_Kagerou 一致：协程只负责演出，buff 结束后的清理交给 onBuffEnd。
        // 不含「凉爽」tag 的料理，评价上限压到「普通」；containsOrNot=false 表示缺少该 tag 时生效。
        scene.Buffs.LimitEvalLevel(
            PunishmentBuff,
            BuffSeconds,
            EvalNormal,
            [CoolTag],
            food: true,
            containsOrNot: false,
            onBuffEnd: () =>
            {
                Vfx.Stop(frost);
                Vfx.Stop(coolDown);
            },
            description: (currentTime, description) => description
                .Replace("$a", scene.Common.FoodTagText(CoolTag))
                .Replace("$b", scene.Common.EvaluationText(EvalNormal))
                .Replace("$c", currentTime.ToString()));

        yield break;

        // 原 EventCoroutineDelegation.Schedule(SetCameraShake(...))：相机震动并等待演出结束。
        IEnumerator Shake()
        {
            scene.Presentation.ShakeCamera(0.35f, 0.35f, 0.4f);
            yield return coroutines.AfterSeconds(0.75f);
        }
    }

    #endregion
}

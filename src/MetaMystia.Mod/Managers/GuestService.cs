using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.RunTime.Common;
using Il2CppInterop.Runtime;
using Il2CppSystem;
using NightScene.GuestManagementUtility;
using NightScene.EventUtility;
using Night.UI.HUD.Ordering;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer.Messages;
using SgrYuki.Utils;

namespace MetaMystia;

[AutoLog]
public static partial class GuestService
{
    /// <summary>
    /// 实现客机对 SpawnNormalGuestGroupExtern 前半部分的重放，并注入 Ids GetFund MaxFundCarry 数据，但跳过了 落座/入队/判定离开 的逻辑以等待后续同步事件
    /// </summary>
    /// <param name="fsm"></param>
    public static void ReplaySpawnNormalGuestGroupExtern(ref GuestFSM fsm, GuestSpawnInfo spawnInfo, IWorkSceneServices services)
    {
        var ids = fsm.Ids;

        if (!EventManager.Instance.ShouldNormalGuestInstantiateBySpecialBuff) return;

        if (ids.Length <= 0 || ids.Length > 2) return;

        // 组由框架按游戏自己的生成路径建立（含图标登记与客人账目）。座位、排队与离场不在这里决定：
        // 客机侧入座闸门关着，框架的生成会在 TrySendToSeat 处短路，与原来手写 PostInitializeGuestGroup 等价。
        var handle = services.Guests.SpawnNormal(
            ids.Select(id => new GuestDescription(id)).ToArray(),
            Request(spawnInfo, spawnInfo.HasNormalSpawnArgs));
        if (handle.IsNone) return;

        fsm.Handle = handle;
        GuestsMap.Bind(fsm);
        ApplyFund(handle, fsm);
    }

    /// <summary>
    /// 实现客机对 SpawnSpecialGuestGroup 前半部分的重放，并注入 Ids GetFund MaxFundCarry 数据，但跳过了 落座/入队/判定离开 的逻辑以等待后续同步事件
    /// </summary>
    /// <param name="fsm"></param>
    public static void ReplaySpawnSpecialGuestGroup(ref GuestFSM fsm, GuestSpawnInfo spawnInfo, IWorkSceneServices services)
    {
        if (!EventManager.Instance.ShouldSpecialGuestInstantiateBySpecialBuff) return;

        var handle = services.Guests.SpawnSpecial(
            fsm.Ids[0],
            Request(spawnInfo, spawnInfo.HasSpecialSpawnArgs));
        if (handle.IsNone) return;

        fsm.Handle = handle;
        GuestsMap.Bind(fsm);
        ApplyFund(handle, fsm);
    }

    /// <summary>
    /// 主机广播的生成参数还原成框架的生成请求；没有生成参数时沿用原版的默认值。
    /// 稀客的生成类型不在框架请求里（原补丁取的是 <c>SpawnSpecialGuestGroup</c> 的形参），沿用原版默认值。
    /// </summary>
    private static GuestSpawnRequest Request(GuestSpawnInfo spawnInfo, bool hasArgs) => new()
    {
        SpawnPosition = spawnInfo.HasOverrideSpawnPosition
            ? new Mystia.Numerics.Vector3(spawnInfo.OverrideSpawnX, spawnInfo.OverrideSpawnY, spawnInfo.OverrideSpawnZ)
            : null,
        LeaveType = hasArgs ? (GuestLeaveType)(int)spawnInfo.LeaveType : GuestLeaveType.Move,
        DeskCode = hasArgs ? spawnInfo.TargetDeskCode : -1,
        Fade = !hasArgs || spawnInfo.ShouldFade,
    };

    /// <summary>主机的资金口径写在生成出来的组上（原实现是直接写控制器字段）。</summary>
    private static void ApplyFund(GuestHandle handle, GuestFSM fsm)
    {
        if (!handle.TryGet(out var guest)) return;
        guest.SetFund(fsm.Fund);
        guest.SetMaxFundCarry(fsm.MaxFundCarry);
    }

    /// <summary>
    /// 注销 OrderController 订单与桌位交互回调。CleanOrderInfo 按 PeekOrders 引用匹配，
    /// 客机重放订单时可能与 HUD 实例不一致，需按 DeskCode 兜底。
    /// </summary>
    public static void CleanGuestOrderRegistration(IWorkSceneServices services, GuestHandle handle)
    {
        if (!handle.TryGet(out var guest)) return;
        var deskCode = guest.DeskCode;

        if (guest.PendingOrderCount > 0)
            services.Guests.CleanOrderInfo(handle);

        if (deskCode == -1) return;

        RemoveHudOrderForDesk(deskCode);
        services.Guests.CleanDeskArrivalCallback(deskCode);
    }

    /// <summary>
    /// FSM 已移除时仍清理 HUD 订单（主机 GuestKillMessage 携带 DeskCode）。
    /// </summary>
    public static void CleanGuestOrderRegistrationForDesk(IWorkSceneServices services, int deskCode)
    {
        if (deskCode == -1) return;
        RemoveHudOrderForDesk(deskCode);
        services.Guests.CleanDeskArrivalCallback(deskCode);
    }

    private static int _removeHudOrderDeskCode;

    private static bool MatchHudOrderDesk(GuestsManager.OrderBase order)
        => order.DeskCode == _removeHudOrderDeskCode;

    private static void RemoveHudOrderForDesk(int deskCode)
    {
        _removeHudOrderDeskCode = deskCode;
        System.Predicate<GuestsManager.OrderBase> match = MatchHudOrderDesk;
        OrderController.RemoveOrder(
            DelegateSupport.ConvertDelegate<Il2CppSystem.Predicate<GuestsManager.OrderBase>>(match),
            "MetaMystia::ForceCleanupGuest");
    }

    /// <summary>
    /// 通用性强制清理。离桌改走服务（内部放行被关掉的离场开关）。
    /// </summary>
    public static void ReplayForceCleanupGuest(IWorkSceneServices services, GuestHandle handle)
    {
        if (!handle.TryGet(out var guest)) return;

        CleanGuestOrderRegistration(services, handle);

        if (guest.HasLeft)
        {
            guest.FlyToSpawn(true);
            return;
        }

        if (guest.DeskCode != -1)
        {
            services.Guests.StopPatientCountdown(handle);
            GuestFSM.TryCloseServePanel(guest.DeskCode);
            // 这是主机清理崩溃顾客、客机重放主机清理命令的路径：不再向对端广播离场。
            // （原 GuestReentryPermits.LeaveFromDesk 令牌由 GuestSync.LeaveBroadcastSuspended 取代）
            Listeners.GuestSync.LeaveBroadcastSuspended = true;
            try
            {
                services.Guests.Leave(handle, GuestLeaveKind.Other);
            }
            finally
            {
                Listeners.GuestSync.LeaveBroadcastSuspended = false;
            }
            return;
        }

        if (guest.IsQueued)
        {
            guest.RemoveFromQueue();
            services.Guests.StopPatientCountdown(handle);
        }

        guest.FlyToSpawn(true);
    }
}

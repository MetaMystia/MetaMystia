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
    public static void ReplaySpawnNormalGuestGroupExtern(ref GuestFSM fsm, GuestSpawnInfo spawnInfo)
    {
        var ids = fsm.Ids;

        if (GuestsManager.Instance == null) return;
        if (!EventManager.Instance.ShouldNormalGuestInstantiateBySpecialBuff) return;

        if (ids.Length <= 0 || ids.Length > 2) return;

        var normalGuests = new Il2CppSystem.Collections.Generic.List<NormalGuest>();
        foreach (var id in ids)
        {
            normalGuests.Add(DataBaseCharacter.RefNGuest(id));
        }

        var postprocessCharacterCallback = GuestsManager.Instance.getPostprocessCharacterCallback.Invoke();
        var overrideSpawnPosition = spawnInfo.HasOverrideSpawnPosition
            ? new Il2CppSystem.Nullable<Vector3>(new Vector3(spawnInfo.OverrideSpawnX, spawnInfo.OverrideSpawnY, spawnInfo.OverrideSpawnZ))
            : new Il2CppSystem.Nullable<Vector3>();
        var leaveType = spawnInfo.HasNormalSpawnArgs
            ? spawnInfo.LeaveType
            : GuestGroupController.LeaveType.Move;
        var targetDeskCode = spawnInfo.HasNormalSpawnArgs ? spawnInfo.TargetDeskCode : -1;
        var shouldFade = !spawnInfo.HasNormalSpawnArgs || spawnInfo.ShouldFade;

        var controller = new NormalGuestsController(
            normalGuests.ToIEnumerable(),
            overrideSpawnPosition,
            postprocessCharacterCallback,
            leaveType);


        GuestsManager.Instance.guestIconManager.Add(controller);
        EventManager.Instance.AddServedGuest(controller.guestInstances.Length, false);

        fsm.Controller = controller;

        // 客机的 PostInitializeGuestGroup 会在执行 TrySendToSeat Prefix 时因返回 true 而被短路
        GuestsManager.Instance.PostInitializeGuestGroup(controller, targetDeskCode, false, shouldFade);

        controller.GetFund = fsm.Fund;
        controller.MaxFundCarry = fsm.MaxFundCarry;
    }

    /// <summary>
    /// 实现客机对 SpawnSpecialGuestGroup 前半部分的重放，并注入 Ids GetFund MaxFundCarry 数据，但跳过了 落座/入队/判定离开 的逻辑以等待后续同步事件
    /// </summary>
    /// <param name="fsm"></param>
    public static void ReplaySpawnSpecialGuestGroup(ref GuestFSM fsm, GuestSpawnInfo spawnInfo)
    {
        if (!EventManager.Instance.ShouldSpecialGuestInstantiateBySpecialBuff) return;

        var specialGuest = DataBaseCharacter.RefSGuest(fsm.Ids[0]);
        var overrideSpawnPosition = spawnInfo.HasOverrideSpawnPosition
            ? new Il2CppSystem.Nullable<Vector3>(new Vector3(spawnInfo.OverrideSpawnX, spawnInfo.OverrideSpawnY, spawnInfo.OverrideSpawnZ))
            : new Il2CppSystem.Nullable<Vector3>();
        var specialGuestsController = new SpecialGuestsController(
            specialGuest,
            overrideSpawnPosition,
            GuestsManager.Instance.getPostprocessCharacterCallback.Invoke(),
            spawnInfo.HasSpecialSpawnArgs ? spawnInfo.LeaveType : GuestGroupController.LeaveType.Move,
            spawnInfo.HasSpecialSpawnArgs ? spawnInfo.GuestSpawnType : SpecialGuestsController.GuestSpawnType.Normal);

        fsm.Controller = specialGuestsController;

        specialGuestsController.OnLeaveCallback = null;
        GuestsManager.Instance.guestIconManager.Add(specialGuestsController);
        if (!RunTimeScheduler.IsChallenge() && specialGuestsController.IsHerself && RunTimeScheduler.ContainsSpecialNPCServeInWorkMission(fsm.Ids[0], out int _))
        {
            GuestsManager.Instance.guestIconManager.SwitchState(specialGuestsController, GuestState.ServeInWorkMissionIcon);
        }

        // recordIzakaya 来源不明，暂不能确定是否需要移除
        // if (recordIzakaya && GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.IzakayaData != null)
        // {
        // 	EventManager.Instance.AddServedGuest(specialGuestsController.guestInstances.Length, true);
        // 	RunTimeAlbum.RecordSpecialGuestIzakaya(id, GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.IzakayaData.Id);
        // }

        // 客机的 PostInitializeGuestGroup 会在执行 TrySendToSeat Prefix 时因返回 true 而被短路
        GuestsManager.Instance.PostInitializeGuestGroup(
            specialGuestsController,
            spawnInfo.HasSpecialSpawnArgs ? spawnInfo.TargetDeskCode : -1,
            false,
            !spawnInfo.HasSpecialSpawnArgs || spawnInfo.ShouldFade);

        specialGuestsController.GetFund = fsm.Fund;
        specialGuestsController.MaxFundCarry = fsm.MaxFundCarry;
    }

    /// <summary>
    /// 注销 OrderController 订单与桌位交互回调。CleanOrderInfo 按 PeekOrders 引用匹配，
    /// 客机重放订单时可能与 HUD 实例不一致，需按 DeskCode 兜底。
    /// </summary>
    public static void CleanGuestOrderRegistration(GuestGroupController controller)
    {
        if (controller == null) return;
        var deskCode = controller.DeskCode;

        if (controller.AllOrdersCount > 0)
            GuestsManager.Instance.CleanOrderInfo(controller);

        if (deskCode == -1) return;

        RemoveHudOrderForDesk(deskCode);
        // 唯一公开入口：仅 Remove(deskCode)，与 DLC4 语义无关
        GuestsManager.Instance.EndDlc4SpecialManualOrder(controller);
    }

    /// <summary>
    /// FSM 已移除时仍清理 HUD 订单（主机 GuestKillMessage 携带 DeskCode）。
    /// </summary>
    public static void CleanGuestOrderRegistrationForDesk(int deskCode)
    {
        if (deskCode == -1) return;
        RemoveHudOrderForDesk(deskCode);
        var guest = GuestsManager.Instance.GetInDeskGuest(deskCode);
        if (guest != null)
            GuestsManager.Instance.EndDlc4SpecialManualOrder(guest);
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
    public static void ReplayForceCleanupGuest(IWorkSceneServices services, GuestGroupController controller)
    {
        if (controller == null) return;

        CleanGuestOrderRegistration(controller);

        if (!controller.HaveNotLeft())
        {
            controller.FlyToSpawn(true);
            return;
        }

        if (controller.DeskCode != -1)
        {
            GuestsManager.Instance.RemoveFromPatientCountdown(controller);
            GuestFSM.TryCloseServePanel(controller.DeskCode);
            // 这是主机清理崩溃顾客、客机重放主机清理命令的路径：不再向对端广播离场。
            // （原 GuestReentryPermits.LeaveFromDesk 令牌由 GuestSync.LeaveBroadcastSuspended 取代）
            Listeners.GuestSync.LeaveBroadcastSuspended = true;
            try
            {
                services.Guests.Leave(controller, GuestLeaveKind.Other);
            }
            finally
            {
                Listeners.GuestSync.LeaveBroadcastSuspended = false;
            }
            return;
        }

        if (controller.queued)
        {
            controller.RemoveFromQueue();
            GuestsManager.Instance.RemoveFromPatientCountdown(controller);
        }

        controller.FlyToSpawn(true);
    }
}

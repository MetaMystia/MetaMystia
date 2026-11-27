using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using Night.UI.HUD.Ordering;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;
using SgrYuki.Utils;

namespace MetaMystia.Multiplayer.Guests;

/// <summary>客机重放原版顾客流程中由主机决定的部分。</summary>
public static class GuestReplay
{
    /// <summary>
    /// 对应 SpawnNormalGuestGroupExtern 与 SpawnSpecialGuestGroup 的前半段：构造控制器并执行 PostInitializeGuestGroup。
    /// 入座、排队或离开由后续事件决定，客机的 TrySendToSeat 在 Hook 中短路。生成以主机为准，本地门牌不参与。
    /// </summary>
    public static void Spawn(GuestRecord guest)
    {
        var info = guest.SpawnInfo;
        var manager = GuestsManager.Instance;
        var overrideSpawnPosition = info.HasOverrideSpawnPosition
            ? new Il2CppSystem.Nullable<Vector3>(new Vector3(info.OverrideSpawnX, info.OverrideSpawnY, info.OverrideSpawnZ))
            : new Il2CppSystem.Nullable<Vector3>();
        var hasArgs = info.GuestType == GuestsManager.GuestType.Normal ? info.HasNormalSpawnArgs : info.HasSpecialSpawnArgs;
        var leaveType = hasArgs ? info.LeaveType : GuestGroupController.LeaveType.Move;
        GuestGroupController controller;
        if (info.GuestType == GuestsManager.GuestType.Normal)
        {
            var normalGuests = new Il2CppSystem.Collections.Generic.List<NormalGuest>();
            foreach (var id in info.Ids) normalGuests.Add(DataBaseCharacter.RefNGuest(id));
            controller = new NormalGuestsController(normalGuests.ToIEnumerable(), overrideSpawnPosition,
                manager.getPostprocessCharacterCallback.Invoke(), leaveType);
            manager.guestIconManager.Add(controller);
            EventManager.Instance.AddServedGuest(controller.guestInstances.Length, false);
        }
        else
        {
            var special = new SpecialGuestsController(DataBaseCharacter.RefSGuest(info.Ids[0]), overrideSpawnPosition,
                manager.getPostprocessCharacterCallback.Invoke(), leaveType,
                hasArgs ? info.GuestSpawnType : SpecialGuestsController.GuestSpawnType.Normal);
            special.OnLeaveCallback = null;
            manager.guestIconManager.Add(special);
            if (!RunTimeScheduler.IsChallenge() && special.IsHerself
                && RunTimeScheduler.ContainsSpecialNPCServeInWorkMission(info.Ids[0], out int _))
                manager.guestIconManager.SwitchState(special, GuestState.ServeInWorkMissionIcon);
            if (info.RecordIzakaya && IzakayaConfigure.Instance.IzakayaData is { } izakaya)
            {
                EventManager.Instance.AddServedGuest(special.guestInstances.Length, true);
                RunTimeAlbum.RecordSpecialGuestIzakaya(info.Ids[0], izakaya.Id);
            }
            controller = special;
        }
        GuestRegistry.Current.Attach(guest, controller);
        manager.PostInitializeGuestGroup(controller, hasArgs ? info.TargetDeskCode : -1, false, !hasArgs || info.ShouldFade);
        controller.GetFund = info.Fund;
        controller.MaxFundCarry = info.MaxFundCarry;
        if (guest.Stats != null) GuestStatsMessage.ApplyTo(controller, guest.Stats);
    }

    /// <summary>
    /// 对应 PostInitializeGuestGroup 的入队出口。排队耐心耗尽由主机决定；断线交还后按原版离开。
    /// </summary>
    public static void Queue(GuestRecord guest, bool tryToJumpQueue)
    {
        var manager = GuestsManager.Instance;
        var controller = guest.Controller;
        System.Action<GuestGroupController> onPatientDepleted = queued =>
        {
            if (GameSession.HasRoomPeers) return;
            manager.RemoveFromPatientCountdown(queued);
            queued.MoveToSpawn();
        };
        System.Action<GuestGroupController> onMoveFinish = queued =>
        {
            if (guest.State != GuestPhase.Queued || !queued.queued) return;
            queued.OnStopInQueueCallback?.Invoke(queued);
            manager.AddToPatientCountdown(queued, onPatientDepleted);
        };
        controller.MoveToQueue(onMoveFinish, tryToJumpQueue);
        manager.SpawnGuest(controller);
        guest.State = GuestPhase.Queued;
    }

    /// <summary>
    /// 对应 TrySendToSeat 的成功分支，目标桌由主机给出。出队时补上 CheckAndSendFromQueue 的收尾。
    /// 原版到座后延迟首单，客机改为等待主机点单。
    /// </summary>
    public static void Seat(GuestRecord guest, int deskCode, bool fromQueue)
    {
        var manager = GuestsManager.Instance;
        var controller = guest.Controller;
        if (guest.State == GuestPhase.Constructed) manager.SpawnGuest(controller);
        manager.occupiedDesks.Add(deskCode);
        guest.State = GuestPhase.Seating;
        guest.Arrived = false;
        controller.MoveToDesk(deskCode, (System.Action)(() => Arrive(guest)));
        manager.Register(manager.AllGuestsControllersInDesk, controller);
        manager.Register(manager.CanPlayerRepellGuest, controller);
        NightScene.SceneManager.Instance.PlayerCharacter.RefreshCurrentFocus();
        if (fromQueue)
        {
            controller.OnLeaveQueueCallback?.Invoke(controller);
            manager.RemoveFromPatientCountdown(controller);
        }
    }

    /// <summary>TrySendToSeat 的到座处理。实际到座与宽限期满的追赶都会调用，只执行一次。</summary>
    public static void Arrive(GuestRecord guest)
    {
        var controller = guest.Controller;
        if (guest.Arrived || guest.IsLeft || !controller.HaveNotLeft()) return;
        guest.Arrived = true;
        controller.IsOrdering = true;
        controller.RefreshCurrentFundAndOrder();
        controller.OnFinishOrderCallback?.Invoke(controller);
        controller.OnSitCallback?.Invoke(controller);
        NightScene.UI.UIManager.Instance.guestBuffMarkModule.TryShowTargetDeskBuffMarkCanvasGroup(controller.DeskCode);
        GuestsManager.Instance.guestIconManager.SwitchState(controller, GuestState.Await);
        if (guest.State == GuestPhase.Seating) guest.State = GuestPhase.Seated;
        if (!GameSession.HasRoomPeers)
            GuestsManager.Instance.StartCoroutine(GuestRegistry.DelayFirstOrder(controller).WrapToIl2Cpp());
    }

    /// <summary>
    /// 未入座离开：排队耐心耗尽、打烊清队、生成后无处可去、符卡驱逐。
    /// 本地打烊已先行清队的顾客不再重复离开。
    /// </summary>
    public static void LeaveUnseated(GuestRecord guest, GuestPhase previous, bool fly, bool fade)
    {
        var controller = guest.Controller;
        if (previous == GuestPhase.Queued && !controller.queued) return;
        GuestsManager.Instance.RemoveFromPatientCountdown(controller);
        if (fly) controller.FlyToSpawn(fade);
        else controller.MoveToSpawn();
    }

    /// <summary>主机已让该桌空出而本地仍被占用时，清理本地占用者。</summary>
    public static void ClearDesk(int deskCode)
    {
        var manager = GuestsManager.Instance;
        var occupant = manager.GetInDeskGuest(deskCode);
        if (occupant == null)
        {
            manager.occupiedDesks.Remove(deskCode);
            return;
        }
        var stale = GuestRegistry.Current.Find(occupant);
        Log.Warning($"Desk {deskCode} still held by {stale?.ToString() ?? "untracked guest"}, clearing");
        if (stale != null) stale.State = GuestPhase.Left;
        ForceCleanup(occupant);
    }

    /// <summary>不经原版流程的强制清理：订单显示、耐心、桌位与面板，然后离场。</summary>
    public static void ForceCleanup(GuestGroupController controller)
    {
        var manager = GuestsManager.Instance;
        var deskCode = controller.DeskCode;
        if (controller.AllOrdersCount > 0) manager.CleanOrderInfo(controller);
        if (deskCode != -1)
        {
            RemoveHudOrder(deskCode);
            manager.EndDlc4SpecialManualOrder(controller);
        }
        if (!controller.HaveNotLeft())
        {
            controller.FlyToSpawn(true);
            return;
        }
        if (deskCode != -1)
        {
            manager.RemoveFromPatientCountdown(controller);
            ServeSlots.ClosePanel(deskCode, GuestRegistry.Current.Find(controller), returnLocal: true);
            GuestsManagerPatch.LeaveFromDesk_ReversePatch(manager, controller, GuestGroupController.LeaveType.Fading, null, false);
            return;
        }
        if (controller.queued)
        {
            controller.RemoveFromQueue();
            manager.RemoveFromPatientCountdown(controller);
        }
        controller.FlyToSpawn(true);
    }

    private static int removeHudOrderDeskCode;

    private static bool MatchHudOrderDesk(GuestsManager.OrderBase order) => order.DeskCode == removeHudOrderDeskCode;

    /// <summary>CleanOrderInfo 按订单引用匹配；客机重放的订单可能与 HUD 实例不同，按桌号兜底。</summary>
    private static void RemoveHudOrder(int deskCode)
    {
        removeHudOrderDeskCode = deskCode;
        System.Predicate<GuestsManager.OrderBase> match = MatchHudOrderDesk;
        OrderController.RemoveOrder(DelegateSupport.ConvertDelegate<Il2CppSystem.Predicate<GuestsManager.OrderBase>>(match),
            "MetaMystia::ForceCleanupGuest");
    }
}

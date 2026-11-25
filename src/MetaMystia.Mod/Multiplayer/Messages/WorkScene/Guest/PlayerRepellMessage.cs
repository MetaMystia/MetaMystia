using System.Linq;

using MemoryPack;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 客机 → 主机：本端已执行原版赶客，在实际离桌时上报分支与赶客后心情。
/// 主机按分支执行有房间效果的部分（热情、连击、付款、排队客耐心），不重判本端 buff；
/// 主机侧顾客已离开或正在评价时忽略。
/// </summary>
[MemoryPackable]
public partial class PlayerRepellMessage : MultiplayerMessage
{
    public int RuntimeId { get; set; }
    public RepelBranch Branch { get; set; }
    public int Mood { get; set; }

    [HostOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var guest = GuestRegistry.Current.Find(RuntimeId);
        var controller = guest?.Controller;
        if (controller == null || guest.Track != GuestTrack.Replicated || !controller.HaveNotLeft()
            || guest.State is not (GuestPhase.Seating or GuestPhase.Seated or GuestPhase.WaitingServe or GuestPhase.Eaten))
        {
            Log.Warning($"Repel of {guest?.ToString() ?? $"#{RuntimeId}"} from {SenderUid} ignored");
            return;
        }
        var manager = GuestsManager.Instance;
        var events = EventManager.Instance;
        if (Branch == RepelBranch.Normal)
        {
            events.PassionEdit(-10f);
            events.LoseAllCombo();
        }
        controller.Mood = Mood;
        guest.LeaveReason = LeaveReason.Repel;
        manager.RepellInternal(controller, out bool haveSeated, GuestGroupController.LeaveType.Move, true);
        if (haveSeated)
        {
            if (Branch == RepelBranch.Normal) manager.PayByMood(controller);
            else if (Branch == RepelBranch.Counted) manager.GuestPay(controller, true);
        }
        if (Branch != RepelBranch.Normal) return;
        foreach (var queued in GuestGroupController.QueuedGuestControllers.ToArray().Where(x => x.HaveNotLeft()))
        {
            queued.AddPatient(-UnityEngine.Random.Range(5, 31));
            if (queued.CurrentPatient != 0) continue;
            manager.TryCloseCurrentDialogBox(queued);
            manager.ShowSeenRepellDialog(queued);
            queued.UpdatePatient();
        }
    }

    /// <summary>
    /// 客机玩家赶客：按本端 buff 判定分支（与原版 PlayerRepell 相同的判定顺序）并记下，
    /// 实际离桌时（琪斯美分支在回调结束后）由 <see cref="Report"/> 上报。禁止赶客的分支不记。
    /// </summary>
    public static void Local(GuestGroupController controller)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null) return;
        var events = EventManager.Instance;
        if (events.TryGetCountedBuffValue((EventManager.BuffType)159) > 0) guest.LocalRepel = RepelBranch.Counted;
        else if (events.CheckTimedBuffExists((EventManager.BuffType)74)) guest.LocalRepel = null;
        else if (!controller.IsFirst && events.CheckTimedBuffExists((EventManager.BuffType)56))
            guest.LocalRepel = GuestsManager.Instance.KisumeRepellCallback != null ? RepelBranch.Kisume : null;
        else guest.LocalRepel = RepelBranch.Normal;
    }

    /// <summary>客机本端赶客进入离桌：上报主机，此后忽略该顾客的主机事件。</summary>
    public static void Report(GuestRecord guest)
    {
        guest.State = GuestPhase.Left;
        guest.Inbox.Clear();
        ServeSlots.ClosePanel(guest.DeskCode, guest, returnLocal: true);
        new PlayerRepellMessage
        {
            RuntimeId = guest.RuntimeId,
            Branch = guest.LocalRepel.Value,
            Mood = guest.Controller.Mood,
        }.Enqueue();
        guest.LocalRepel = null;
    }
}

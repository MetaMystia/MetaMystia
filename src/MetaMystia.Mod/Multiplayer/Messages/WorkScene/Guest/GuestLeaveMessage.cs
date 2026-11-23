using MemoryPack;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机：顾客离场。离桌（LeaveFromDesk 入口，原因由先前的耐心耗尽或赶客入口标记）、
/// 未入座离开（MoveToSpawn / FlyToSpawn 入口）和强制清理共用。
/// 离场越过该顾客尚未应用的事件，客机重放原版离场以完成桌位、队列与回调清理。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestLeaveMessage : MultiplayerMessage, IGuestEvent
{
    public int RuntimeId { get; set; }
    public LeaveReason Reason { get; set; }
    public GuestGroupController.LeaveType LeaveType { get; set; }
    /// <summary>未入座离开：飞走（FlyToSpawn）而非走回出口。</summary>
    public bool Fly { get; set; }
    public bool Fade { get; set; }
    public int Mood { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => GuestRegistry.Current.Post(this);

    /// <summary>本地仍在吃（评价后续处理未到）时等待，宽限期满后直接离场。</summary>
    public bool Apply(GuestRecord guest, bool force)
    {
        if (guest.IsLeft) return true;
        if (guest.State == GuestPhase.Evaluating && Reason != LeaveReason.Force && !force) return false;
        var controller = guest.Controller;
        var previous = guest.State;
        guest.State = GuestPhase.Left;
        if (controller == null) return true;
        if (Reason == LeaveReason.Force)
        {
            GuestReplay.ForceCleanup(controller);
            return true;
        }
        if (!controller.HaveNotLeft()) return true;
        if (previous is GuestPhase.Constructed or GuestPhase.Queued)
        {
            GuestReplay.LeaveUnseated(guest, previous, Fly, Fade);
            return true;
        }
        ServeSlots.ClosePanel(guest.DeskCode, guest, returnLocal: true);
        var manager = GuestsManager.Instance;
        using (Bypass.Enter(controller))
        {
            switch (Reason)
            {
                case LeaveReason.PatientDepleted when previous == GuestPhase.WaitingServe:
                    manager.PatientDepletedLeave(controller);
                    break;
                case LeaveReason.Repel:
                    controller.Mood = Mood;
                    manager.RepellInternal(controller, out _, LeaveType, false);
                    break;
                default:
                    manager.LeaveFromDesk(controller, LeaveType, null, false);
                    break;
            }
        }
        return true;
    }

    /// <summary>主机离桌入口。原因来自先前入口的标记，默认为正常离桌。</summary>
    public static void DetectDesk(GuestGroupController controller, GuestGroupController.LeaveType leaveType)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null || guest.Track != GuestTrack.Replicated || guest.IsLeft) return;
        Send(guest, guest.LeaveReason, leaveType);
        ServeSlots.ClosePanel(controller.DeskCode, guest, returnLocal: true);
    }

    /// <summary>主机未入座离开入口。入座后的离场由离桌入口负责，这里只处理生成或排队中的顾客。</summary>
    public static void DetectUnseated(GuestGroupController controller, bool fly, bool fade)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null || guest.Track != GuestTrack.Replicated) return;
        if (guest.State is not (GuestPhase.Constructed or GuestPhase.Queued)) return;
        Send(guest, LeaveReason.Unseated, GuestGroupController.LeaveType.Move, fly, fade);
    }

    public static void Send(GuestRecord guest, LeaveReason reason, GuestGroupController.LeaveType leaveType, bool fly = false, bool fade = false)
    {
        guest.State = GuestPhase.Left;
        guest.EvalPending = false;
        new GuestLeaveMessage
        {
            RuntimeId = guest.RuntimeId,
            Reason = reason,
            LeaveType = leaveType,
            Fly = fly,
            Fade = fade,
            Mood = guest.Controller?.Mood ?? 0,
        }.Enqueue();
    }
}

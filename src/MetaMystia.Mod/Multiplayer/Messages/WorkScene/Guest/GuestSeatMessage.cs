using MemoryPack;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机：顾客分配到桌位（TrySendToSeat 成功），包括生成即入座和出队入座。</summary>
[MemoryPackable]
public partial class GuestSeatMessage : MultiplayerMessage, IGuestEvent
{
    public int RuntimeId { get; set; }
    public int DeskCode { get; set; }
    public bool FromQueue { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => GuestRegistry.Current.Post(this);

    /// <summary>
    /// 本地桌位仍被占用时等待；宽限期满后以主机为准清理占用的同步顾客再入座。本端剧情顾客占桌时继续等待。
    /// </summary>
    public bool Apply(GuestRecord guest, bool force)
    {
        if (guest.State is not (GuestPhase.Constructed or GuestPhase.Queued)) return true;
        if (guest.Controller == null) return force;
        if (!DeskAvailable(guest.Controller))
        {
            if (!force) return false;
            var occupant = GuestsManager.Instance.GetInDeskGuest(DeskCode);
            if (occupant != null && GuestRegistry.Current.Find(occupant) == null) return false;
            GuestReplay.ClearDesk(DeskCode);
        }
        GuestReplay.Seat(guest, DeskCode, FromQueue && guest.State == GuestPhase.Queued);
        return true;
    }

    private bool DeskAvailable(GuestGroupController controller) =>
        GuestsManager.Instance.TrueAvailableDesks.TryGetValue(DeskCode, out var capacity)
        && capacity >= controller.guestInstances.Length;

    public static void Detect(GuestGroupController controller)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null) return;
        var fromQueue = guest.State == GuestPhase.Queued;
        if (guest.State is not (GuestPhase.Constructed or GuestPhase.Queued))
            Log.Warning($"Guest {guest} seated from unexpected phase");
        guest.State = GuestPhase.Seating;
        new GuestSeatMessage { RuntimeId = guest.RuntimeId, DeskCode = controller.DeskCode, FromQueue = fromQueue }.Enqueue();
    }
}

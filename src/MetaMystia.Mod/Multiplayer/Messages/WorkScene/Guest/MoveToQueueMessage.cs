using MemoryPack;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机：顾客入队（MoveToQueue 之后）。</summary>
[MemoryPackable]
[AutoLog]
public partial class MoveToQueueMessage : MultiplayerMessage, IGuestEvent
{
    public int RuntimeId { get; set; }
    public bool TryToJumpQueue { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => GuestRegistry.Current.Post(this);

    public bool Apply(GuestRecord guest, bool force)
    {
        if (guest.State == GuestPhase.Constructed && guest.Controller != null) GuestReplay.Queue(guest, TryToJumpQueue);
        return true;
    }

    public static void Detect(GuestGroupController controller, bool tryToJumpQueue)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null) return;
        if (guest.State != GuestPhase.Constructed) Log.Warning($"Guest {guest} queued from unexpected phase");
        guest.State = GuestPhase.Queued;
        new MoveToQueueMessage { RuntimeId = guest.RuntimeId, TryToJumpQueue = tryToJumpQueue }.Enqueue();
    }
}

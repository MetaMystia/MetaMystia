using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class MoveToQueueMessage : MultiplayerMessage
{

    public int RuntimeId { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        QueueForGuest(fsm, nameof(GuestFSM.DoMoveToQueue),
            () => GuestFSM.DoMoveToQueue(rid));
    }

    public static void Send(int runtimeId) =>
        new MoveToQueueMessage { RuntimeId = runtimeId }.Enqueue();
}

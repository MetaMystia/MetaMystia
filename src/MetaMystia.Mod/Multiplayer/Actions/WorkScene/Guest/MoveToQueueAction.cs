using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class MoveToQueueAction : Action
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
        new MoveToQueueAction { RuntimeId = runtimeId }.Enqueue();
}

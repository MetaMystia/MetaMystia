using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class SendFromQueueAction : Action
{
    public int RuntimeId { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        QueueForGuest(fsm, nameof(GuestFSM.DoSendFromQueue),
            () => GuestFSM.DoSendFromQueue(rid));
    }

    public static void Send(int runtimeId) =>
        new SendFromQueueAction { RuntimeId = runtimeId }.Enqueue();
}

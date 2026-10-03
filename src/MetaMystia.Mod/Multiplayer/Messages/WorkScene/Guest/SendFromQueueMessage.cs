using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class SendFromQueueMessage : MultiplayerMessage
{
    public int RuntimeId { get; set; }

    protected override bool ClientOnlyReceive => true;
    protected override bool DiscardOnStory => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        QueueForGuest(fsm, nameof(GuestFSM.DoSendFromQueue),
            () => GuestFSM.DoSendFromQueue(rid));
    }

    public static void Send(int runtimeId) =>
        new SendFromQueueMessage { RuntimeId = runtimeId }.Enqueue();
}

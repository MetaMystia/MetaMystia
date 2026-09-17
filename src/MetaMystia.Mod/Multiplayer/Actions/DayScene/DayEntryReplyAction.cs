using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class DayEntryReplyAction : Action
{
    public int Round { get; set; }
    public bool Ready { get; set; }

    [HostOnlyReceive]
    public override void OnReceivedDerived() => DayDestinationManager.ReceiveEntryReply(SenderUid, Round, Ready);

    public static void Send(int round, bool ready) => new DayEntryReplyAction { Round = round, Ready = ready }.Enqueue();
}

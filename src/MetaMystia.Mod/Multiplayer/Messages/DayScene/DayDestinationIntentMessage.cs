using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class DayDestinationIntentMessage : MultiplayerMessage
{
    public int Round { get; set; }
    public DayDestination Destination { get; set; }

    [HostOnlyReceive]
    public override void OnReceivedDerived() => DayDestinationManager.ReceiveIntent(SenderUid, Round, Destination);

    public static void Send(int round, DayDestination destination) =>
        new DayDestinationIntentMessage { Round = round, Destination = destination }.Enqueue();
}

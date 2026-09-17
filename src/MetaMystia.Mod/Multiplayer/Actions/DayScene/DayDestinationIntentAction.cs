using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class DayDestinationIntentAction : Action
{
    public int Round { get; set; }
    public DayDestination Destination { get; set; }

    [HostOnlyReceive]
    public override void OnReceivedDerived() => DayDestinationManager.ReceiveIntent(SenderUid, Round, Destination);

    public static void Send(int round, DayDestination destination) =>
        new DayDestinationIntentAction { Round = round, Destination = destination }.Enqueue();
}

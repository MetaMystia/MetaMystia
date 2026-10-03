using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class DayDestinationConfirmMessage : MultiplayerMessage
{
    public int Round { get; set; }
    public DayDestination Destination { get; set; }

    protected override bool RequireHostSender => true;
    protected override bool ClientOnlyReceive => true;

    public override void OnReceivedDerived() => DayDestinationManager.ApplyConfirmation(Round, Destination);

    public static void Send(int round, DayDestination destination) =>
        new DayDestinationConfirmMessage { Round = round, Destination = destination }.Enqueue();
}

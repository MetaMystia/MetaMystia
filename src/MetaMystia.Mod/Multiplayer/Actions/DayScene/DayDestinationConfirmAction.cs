using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class DayDestinationConfirmAction : Action
{
    public int Round { get; set; }
    public DayDestination Destination { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.DayScene)]
    public override void OnReceivedDerived() => DayDestinationManager.ApplyConfirmation(Round, Destination);

    public static void Send(int round, DayDestination destination) =>
        new DayDestinationConfirmAction { Round = round, Destination = destination }.Enqueue();
}

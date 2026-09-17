using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

public enum EntryStep { Prepare, Release, Cancel }

[MemoryPackable]
[AutoLog]
public partial class DayDestinationConfirmAction : Action
{
    public int Round { get; set; }
    public DayDestination Destination { get; set; }
    public EntryStep Step { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    public override void OnReceivedDerived()
    {
        switch (Step)
        {
            case EntryStep.Prepare: DayDestinationManager.PrepareEntry(Round, Destination); break;
            case EntryStep.Release: DayDestinationManager.ApplyConfirmation(Round, Destination); break;
            case EntryStep.Cancel: DayDestinationManager.CancelEntry(Round); break;
        }
    }

    public static void Send(int round, DayDestination destination, EntryStep step) =>
        new DayDestinationConfirmAction { Round = round, Destination = destination, Step = step }.Enqueue();
}

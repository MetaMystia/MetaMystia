using System.Collections.Generic;

using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class DayDestinationStateAction : Action
{
    public int Round { get; set; }
    public Dictionary<int, DayDestination> Intents { get; set; } = new();

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.DayScene)]
    public override void OnReceivedDerived() => DayDestinationManager.ApplyState(Round, Intents);

    public static void Send(int round, Dictionary<int, DayDestination> intents) =>
        new DayDestinationStateAction { Round = round, Intents = intents }.Enqueue();
}

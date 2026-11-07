using System.Collections.Generic;

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class DayDestinationStateMessage : MultiplayerMessage
{
    public int Round { get; set; }
    public Dictionary<int, DayDestination> Intents { get; set; } = new();

    [RequireHostSender]
    [ClientOnlyReceive]
    public override void OnReceivedDerived() => DayDestinationManager.ApplyState(Round, Intents);

    public static void Send(int round, Dictionary<int, DayDestination> intents) =>
        new DayDestinationStateMessage { Round = round, Intents = intents }.Enqueue();
}

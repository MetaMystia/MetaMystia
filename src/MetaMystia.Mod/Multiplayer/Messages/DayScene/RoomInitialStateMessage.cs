using System.Collections.Generic;

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>入房后的玩法初始状态，由房主提供。</summary>
[MemoryPackable]
[AutoLog]
public partial class RoomInitialStateMessage : MultiplayerMessage
{
    public int Round { get; set; }
    public Dictionary<int, DayDestination> Intents { get; set; } = new();

    [RequireHostSender]
    [ClientOnlyReceive]
    public override void OnReceivedDerived() => DayDestinationManager.InitializeSession(Round, Intents);

    public static void Send(int uid) => new RoomInitialStateMessage
    {
        Round = DayDestinationManager.Round, Intents = DayDestinationManager.Snapshot(), WireTargetUid = uid
    }.Enqueue();
}

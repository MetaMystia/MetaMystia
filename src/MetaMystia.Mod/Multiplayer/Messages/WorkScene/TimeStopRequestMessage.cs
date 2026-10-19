using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 客机 → 房主：本地是否被面板占住，以最新一条为准。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class TimeStopRequestMessage : MultiplayerMessage
{
    public bool Freeze { get; set; }

    [HostOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => RoomTimeStop.ReceiveDemand(SenderUid, Freeze);

    public static void Send(bool freeze) => new TimeStopRequestMessage { Freeze = freeze }.Enqueue();
}

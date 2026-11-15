using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 房主 → 房间：房间时停裁定结果，以最新一条为准。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class TimeStopStateMessage : MultiplayerMessage
{
    public bool Freeze { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => RoomTimeStop.Apply(Freeze);

    public static void Send(bool freeze) => new TimeStopStateMessage { Freeze = freeze }.Enqueue();
}

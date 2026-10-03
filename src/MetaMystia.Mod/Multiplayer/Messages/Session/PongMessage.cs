using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
public partial class PongMessage : MultiplayerMessage
{
    public int Id { get; set; }

    protected override Mystia.LogLevel OnReceiveLogLevel => Mystia.LogLevel.Debug;
    protected override Mystia.LogLevel OnSendLogLevel => Mystia.LogLevel.Debug;

    protected override bool ClientOnlyReceive => true;

    public override void OnReceivedDerived()
    {
        RoomClock.Receive(Id);
    }

    /// <summary>
    /// 主机回复 Ping，客机据此估算往返延迟。
    /// </summary>
    public static void Send(int id, int uid) =>
        new PongMessage { Id = id, WireTargetUid = uid }.Enqueue();
}

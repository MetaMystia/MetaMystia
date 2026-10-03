using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
public partial class PingMessage : MultiplayerMessage
{
    public int Id { get; set; }
    protected override Mystia.LogLevel OnReceiveLogLevel => Mystia.LogLevel.Debug;
    protected override Mystia.LogLevel OnSendLogLevel => Mystia.LogLevel.Debug;
    public override void OnReceivedDerived()
    {
        PongMessage.Send(Id, SenderUid);
    }

    /// <summary>
    /// 客机→主机发送 Ping
    /// </summary>
    public static void Send(int id) =>
        new PingMessage { Id = id }.Enqueue();
}

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
public partial class PingMessage : MultiplayerMessage
{
    public int Id { get; set; }
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;
    public override void OnReceivedDerived()
    {
        PongMessage.Send(Id, RoomClock.Now, SenderUid);
    }

    /// <summary>
    /// 客机→主机发送 Ping
    /// </summary>
    public static void Send(int id) =>
        new PingMessage { Id = id }.Enqueue();
}

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
public partial class PongMessage : MultiplayerMessage
{
    public int Id { get; set; }

    /// <summary>
    /// 主机收到对应 Ping 那一刻的 NowMs。客机据此估算本地与主机的时钟偏移。
    /// </summary>
    public long HostReceivedMs { get; set; }

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;

    [ClientOnlyReceive]
    public override void OnReceivedDerived()
    {
        RoomClock.Receive(Id, HostReceivedMs);
    }

    /// <summary>
    /// 主机收到 Ping 后回复 Pong，携带主机收到 Ping 那一刻的时间戳。
    /// </summary>
    public static void Send(int id, long hostReceivedMs, int uid) =>
        new PongMessage { Id = id, HostReceivedMs = hostReceivedMs, WireTargetUid = uid }.Enqueue();
}

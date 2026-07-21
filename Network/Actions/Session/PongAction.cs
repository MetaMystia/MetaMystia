using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class PongAction : Action
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
        var sentMs = MpWire.UpdateLatency(Id);
        if (sentMs is long t && HostReceivedMs > 0)
            MpWire.UpdateTimeOffset(HostReceivedMs, t);
    }

    /// <summary>
    /// 主机收到 Ping 后回复 Pong，携带主机收到 Ping 那一刻的时间戳。
    /// </summary>
    public static void Send(int id, long hostReceivedMs) =>
        new PongAction { Id = id, HostReceivedMs = hostReceivedMs }.Enqueue();
}

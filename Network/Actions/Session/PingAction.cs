using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class PingAction : Action
{
    public int Id { get; set; }
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;
    public override void OnReceivedDerived()
    {
        PongAction.Send(Id, MpWire.NowMs);
    }

    /// <summary>
    /// 客机→主机发送 Ping
    /// </summary>
    public static void Send(int id) =>
        new PingAction { Id = id }.Enqueue();
}

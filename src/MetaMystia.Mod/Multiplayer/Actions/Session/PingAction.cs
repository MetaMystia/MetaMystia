using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
public partial class PingAction : Action
{
    public int Id { get; set; }
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;
    public override void OnReceivedDerived()
    {
        PongAction.Send(Id, RoomClock.Now, SenderUid);
    }

    /// <summary>
    /// 客机→主机发送 Ping
    /// </summary>
    public static void Send(int id) =>
        new PingAction { Id = id }.Enqueue();
}

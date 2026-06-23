namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PongBehavior
{
    /// <summary>
    /// 主机收到 Ping 后回复 Pong，携带主机收到 Ping 那一刻的时间戳。
    /// </summary>
    public static void Send(int id, long hostReceivedMs) =>
        new PongAction { Id = id, HostReceivedMs = hostReceivedMs }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PongAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(PongAction action)
    {
        var sentMs = MpWire.UpdateLatency(action.Id);
        if (sentMs is long t && action.HostReceivedMs > 0)
            MpWire.UpdateTimeOffset(action.HostReceivedMs, t);
    }
}

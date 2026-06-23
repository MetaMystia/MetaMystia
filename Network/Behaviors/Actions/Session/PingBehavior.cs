namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PingBehavior
{
    /// <summary>
    /// 客机→主机发送 Ping。
    /// </summary>
    public static void Send(int id) =>
        new PingAction { Id = id }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PingAction>(Handle);
    }

    private static void Handle(PingAction action)
    {
        PongBehavior.Send(action.Id, MpWire.NowMs);
    }
}

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class LeaveRoomBehavior
{
    public static void Send() =>
        new LeaveRoomAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<LeaveRoomAction>(Handle,
            receiveScope: NetReceiveScope.EndpointOnly);
    }

    private static void Handle(LeaveRoomAction _) { }
}

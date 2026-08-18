namespace MetaMystia.Network;

[NetActionBehavior]
internal static class LeaveServerBehavior
{
    public static void Send() => new LeaveServerAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<LeaveServerAction>(Handle,
            receiveScope: NetReceiveScope.EndpointOnly);
    }

    private static void Handle(LeaveServerAction action)
    {
        if (MpManager.IsServerEndpoint)
            MpWire.DisconnectClient(action.SenderUid, notify: false, leaveReason: RoomLeaveReason.Voluntary);
    }
}

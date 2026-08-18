namespace MetaMystia.Network;

[NetActionBehavior]
internal static class JoinRoomRequestBehavior
{
    public static void Send(ushort roomId) =>
        new JoinRoomRequestAction
        {
            RoomId = roomId,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<JoinRoomRequestAction>(Handle,
            receiveScope: NetReceiveScope.EndpointOnly);
    }

    private static void Handle(JoinRoomRequestAction action) =>
        RoomRequestRejectBehavior.Send(action.SenderUid, RoomRequestRejectReason.RoomRequestUnsupported);
}

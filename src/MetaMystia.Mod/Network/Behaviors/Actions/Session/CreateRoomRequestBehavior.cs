namespace MetaMystia.Network;

[NetActionBehavior]
internal static class CreateRoomRequestBehavior
{
    public static void Send() =>
        new CreateRoomRequestAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<CreateRoomRequestAction>(Handle,
            receiveScope: NetReceiveScope.EndpointOnly);
    }

    private static void Handle(CreateRoomRequestAction action) =>
        RoomRequestRejectBehavior.Send(action.SenderUid, RoomRequestRejectReason.RoomRequestUnsupported);
}

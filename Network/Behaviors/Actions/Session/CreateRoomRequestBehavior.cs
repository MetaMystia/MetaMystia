namespace MetaMystia.Network;

[NetActionBehavior]
internal static class CreateRoomRequestBehavior
{
    public static void Send() =>
        new CreateRoomRequestAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<CreateRoomRequestAction>(Handle,
            receiveScope: NetReceiveScope.HostOnly);
    }

    private static void Handle(CreateRoomRequestAction action)
    {
        // 仅服务端端点处理；客机收到此请求一律拒绝。
        RejectBehavior.SendOnly(action.SenderUid, RejectReason.RoomRequestUnsupported);
    }
}

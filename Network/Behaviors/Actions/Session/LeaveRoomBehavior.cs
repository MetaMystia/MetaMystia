namespace MetaMystia.Network;

[NetActionBehavior]
internal static class LeaveRoomBehavior
{
    public static void Send() =>
        new LeaveRoomAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<LeaveRoomAction>(Handle,
            receiveScope: NetReceiveScope.HostOnly);
    }

    private static void Handle(LeaveRoomAction action)
    {
        if (!MpManager.Session.IsRelay)
        {
            RejectBehavior.SendOnly(action.SenderUid, RejectReason.RoomRequestUnsupported);
            return;
        }

        RoomKickBehavior.Send(action.SenderUid, RejectReason.KickedFromRoom);
    }
}

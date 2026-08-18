using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomRequestRejectBehavior
{
    public static void Send(int uid, RoomRequestRejectReason reason) =>
        new RoomRequestRejectAction
        {
            SenderUid = MpConstants.HostUid,
            Reason = reason,
            WireTargetUid = uid,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RoomRequestRejectAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(RoomRequestRejectAction action)
    {
        if (action.SenderUid != MpConstants.HostUid
            || !MpManager.IsInPublicScope
            || !MpWire.Session.RoomRequestPending)
            return;
        MpManager.EndRoomRequest();
        InGameConsole.ShowPassiveFromAnyThread(action.Reason switch
        {
            RoomRequestRejectReason.RoomRequestUnsupported => TextId.RoomRequestUnsupported.Get(),
            RoomRequestRejectReason.RoomNotFound => TextId.RoomNotFound.Get(),
            RoomRequestRejectReason.RoomFull => TextId.RoomFull.Get(),
            RoomRequestRejectReason.RoomIdExhausted => TextId.RoomIdExhausted.Get(),
            _ => TextId.MpDisconnected.Get(),
        });
    }
}

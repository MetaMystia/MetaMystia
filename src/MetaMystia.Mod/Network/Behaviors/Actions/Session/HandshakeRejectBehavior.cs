using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class HandshakeRejectBehavior
{
    public static void SendAndDisconnect(int uid, HandshakeRejectReason reason)
    {
        new HandshakeRejectAction
        {
            SenderUid = MpConstants.HostUid,
            Reason = reason,
            WireTargetUid = uid,
        }.Enqueue();
        MpWire.DisconnectClient(uid, notify: false);
    }

    public static void ShowAndDisconnect(HandshakeRejectReason reason)
    {
        Show(reason);
        MpWire.DisconnectPeer();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<HandshakeRejectAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(HandshakeRejectAction action)
    {
        if (!MpWire.Session.IsConnecting || action.SenderUid != MpConstants.HostUid)
            return;
        ShowAndDisconnect(action.Reason);
    }

    private static void Show(HandshakeRejectReason reason) =>
        InGameConsole.ShowPassiveFromAnyThread(reason switch
        {
            HandshakeRejectReason.ModVersionMismatch => TextId.ModVersionMismatch.Get(),
            HandshakeRejectReason.GameVersionMismatch => TextId.GameVersionMismatch.Get(),
            HandshakeRejectReason.GameResourcesNotLoaded => TextId.GameResourcesNotLoaded.Get(),
            HandshakeRejectReason.PrepWorkReconnectBlocked => TextId.PrepWorkReconnectBlocked.Get(),
            HandshakeRejectReason.ServerFull => TextId.RoomFull.Get(),
            HandshakeRejectReason.InvalidPlayerId => TextId.MpPlayerIdInvalid.Get(),
            HandshakeRejectReason.DuplicatePlayerId => TextId.DuplicatePeerId.Get(),
            HandshakeRejectReason.UnsupportedServerMode => TextId.UnsupportedServerMode.Get(),
            _ => TextId.MpDisconnected.Get(),
        });
}

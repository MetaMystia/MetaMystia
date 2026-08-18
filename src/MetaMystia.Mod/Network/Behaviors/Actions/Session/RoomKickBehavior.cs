using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomKickBehavior
{
    public static void Send(int targetUid, ushort roomId, RoomKickReason reason) =>
        new RoomKickAction
        {
            SenderUid = PlayerManager.Local.Uid,
            TargetUid = targetUid,
            RoomId = roomId,
            Reason = reason,
            WireTargetUid = targetUid,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RoomKickAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(RoomKickAction action)
    {
        if (!MpManager.IsInRoom
            || action.SenderUid != MpWire.Session.HostUid
            || action.TargetUid != PlayerManager.Local.Uid
            || action.RoomId != PlayerManager.Local.RoomId)
            return;

        Plugin.Instance?.Log.LogWarning($"Kicked from room: {action.Reason}");
        InGameConsole.ShowPassiveFromAnyThread(TextId.KickedFromRoom.Get());
        if (!MpWire.Session.IsRelay)
        {
            MpWire.DisconnectPeer();
            return;
        }

        PlayerManager.ClearRoomPeers();
        MpWire.Session.LeaveRelayRoomToPublic();
        PlayerManager.Local.RoomId = MpConstants.PublicRoomId;
        PlayerManager.Local.Role = WireRoomRole.None;
        MpWire.OnRelayPublicEntered();
    }
}

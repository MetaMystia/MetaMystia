using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomKickBehavior
{
    public static void Send(int targetUid, RejectReason reason, params string[] args) =>
        new RoomKickAction
        {
            SenderUid = MpConstants.HostUid,
            Reason = reason,
            Args = args,
            WireTargetUid = targetUid,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RoomKickAction>(Handle);
    }

    private static void Handle(RoomKickAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        var reason = RejectBehavior.FormatReason(action.Reason, action.Args);
        Plugin.Instance?.Log.LogWarning($"Kicked from room: {reason}");
        InGameConsole.ShowPassiveFromAnyThread(reason);
        if (!MpWire.Session.IsRelay)
        {
            // direct 模式下 host 用 Reject(KickedFromServer) 踢人，client 不会收到 RoomKick；
            // 此分支为防御性保留：若 direct client 意外收到 RoomKick，退化为断连。
            MpWire.DisconnectPeer();
            return;
        }

        MpWire.Session.LeaveRelayRoomToPublic();
        PlayerManager.ClearRoomPeers();
        MpWire.OnRelayPublicEntered();
    }
}

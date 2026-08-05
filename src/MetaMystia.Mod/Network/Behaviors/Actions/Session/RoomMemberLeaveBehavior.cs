using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomMemberLeaveBehavior
{
    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RoomMemberLeaveAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(RoomMemberLeaveAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        if (!PlayerManager.PlayerTable.TryGetValue(action.Uid, out var peer))
            return;

        bool wasRoomPeer = PlayerManager.IsRoomPeer(action.Uid);
        peer.ApplyResources(null);
        peer.RoomId = MpConstants.PublicRoomId;
        peer.Role = WireRoomRole.None;
        if (wasRoomPeer)
            PlayerManager.HidePeer(action.Uid);

        if (action.Reason == RoomLeaveReason.Voluntary && wasRoomPeer)
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(action.Uid, peer.Id)));
    }
}

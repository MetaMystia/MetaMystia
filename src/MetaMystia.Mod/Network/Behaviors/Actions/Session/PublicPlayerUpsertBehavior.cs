using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PublicPlayerUpsertBehavior
{
    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PublicPlayerUpsertAction>(Handle);
    }

    private static void Handle(PublicPlayerUpsertAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        var player = action.Player;
        if (player == null)
            return;

        if (player.Uid == PlayerManager.Local.Uid)
        {
            if (player.RoomId == MpConstants.PublicRoomId && !MpManager.IsInRoom)
                return;
            if (player.Role == WireRoomRole.Host && MpManager.IsInRoom)
            {
                PlayerManager.Local.Role = WireRoomRole.Host;
                MpWire.Session.AssignHostUid(PlayerManager.Local.Uid);
                return;
            }
            if (player.RoomId == MpConstants.PublicRoomId && MpManager.IsInRoom)
            {
                PlayerManager.ClearRoomPeers();
                MpWire.Session.LeaveRelayRoomToPublic();
                PlayerManager.Local.RoomId = MpConstants.PublicRoomId;
                PlayerManager.Local.Role = WireRoomRole.None;
                MpWire.OnRelayPublicEntered();
            }
            return;
        }

        bool wasRoomPeer = PlayerManager.IsRoomPeer(player.Uid);
        PlayerManager.UpsertLitePlayer(player);
        PlayerManager.TryEnsureDayScenePeer(player.Uid);

        if (wasRoomPeer && !PlayerManager.IsRoomPeer(player.Uid))
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(player.Uid, player.PeerId)));
    }
}

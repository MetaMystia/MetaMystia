using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PlayerPresenceBehavior
{
    public static void SendLocal()
    {
        new PlayerPresenceAction
        {
            Player = PlayerManager.Local.ToLiteData(),
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PlayerPresenceAction>(Handle);
    }

    private static void Handle(PlayerPresenceAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        var player = action.Player;
        if (player == null || player.Uid == PlayerManager.Local.Uid)
            return;

        bool wasRoomPeer = PlayerManager.IsRoomPeer(player.Uid);
        PlayerManager.UpsertLitePlayer(player);

        // 非 DayScene 仅更新 PlayerTable；DayScene 尝试创建 NPC。
        PlayerManager.TryEnsureDayScenePeer(player.Uid);

        if (wasRoomPeer && !PlayerManager.IsRoomPeer(player.Uid))
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(player.Uid, player.PeerId)));
    }
}

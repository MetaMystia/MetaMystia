using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PlayerPresenceBehavior
{
    public static void SendLocal()
    {
        new PlayerPresenceAction
        {
            Uid = PlayerManager.Local.Uid,
            PeerId = PlayerManager.Local.Id,
            RoomId = MpWire.Session.RoomId,
            Scene = MpManager.LocalScene.ToWire(),
            Skin = PlayerManager.Local.Skin,
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

        if (action.Uid == PlayerManager.Local.Uid)
            return;

        bool wasRoomPeer = PlayerManager.IsRoomPeer(action.Uid);
        PlayerManager.UpsertPresence(action);

        // 非 DayScene 仅更新 PlayerTable；DayScene 尝试创建 NPC。
        PlayerManager.TryEnsureDayScenePeer(action.Uid);

        if (wasRoomPeer && !PlayerManager.IsRoomPeer(action.Uid))
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(action.Uid, action.PeerId)));
    }
}

using Common.UI;
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
        // PlayerPresence 由服务端端点（uid=0）权威下发；relay 房主也会收到，故按端点 uid 校验。
        if (action.SenderUid != MpConstants.HostUid)
            return;

        if (action.Uid == PlayerManager.Local.Uid)
            return;

        bool wasRoomPeer = PlayerManager.Peers.ContainsKey(action.Uid);
        bool wasVisible = PlayerManager.TryGetVisiblePeer(action.Uid, out _);
        var peer = PlayerManager.UpsertPresence(action);

        // 场景驱动 spawn：DayScene 下为所有在线玩家生成，WorkScene 下仅同房间生成。
        bool sameScopeVisible = PlayerManager.IsSameRoom(action.RoomId)
            || (MpManager.Session.IsInPublicScope && action.RoomId == MpConstants.PublicRoomId);
        if (peer != null
            && sameScopeVisible
            && (!wasVisible || wasRoomPeer != PlayerManager.Peers.ContainsKey(action.Uid))
            && MpManager.LocalScene is Scene.DayScene or Scene.WorkScene)
        {
            peer.ResetMotion();
            peer.SpawnForScene();
        }

        if (wasRoomPeer && !PlayerManager.Peers.ContainsKey(action.Uid))
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(action.Uid, action.PeerId)));
    }
}

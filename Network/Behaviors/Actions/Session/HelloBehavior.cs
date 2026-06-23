using Common.UI;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class HelloBehavior
{
    /// <summary>
    /// 客机发送 Hello 给主机请求连接。
    /// </summary>
    public static void Send()
    {
        PlayerInfoData peerInfo = new()
        {
            Uid = -1,
            PeerId = MpManager.PlayerId,
            IncrementalDataBase = PlayerManager.Local.IncrementalDataBase,
            Skin = PlayerManager.Local.Skin,
            IsDayOver = PlayerManager.LocalIsDayOver,
            IsPrepOver = PlayerManager.LocalIsPrepOver
        };

        new HelloAction
        {
            PeerInfo = peerInfo,
            Version = Plugin.ModVersion,
            CurrentGameScene = MpManager.LocalScene.ToWire(),
            GameVersion = Plugin.GameVersion,
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<HelloAction>(Handle,
            receiveScope: NetReceiveScope.HostOnly);
    }

    private static void Handle(HelloAction action)
    {
        if (action.Version != Plugin.ModVersion)
        {
            Plugin.Instance?.Log.LogError($"Mod version mismatch! Local: {Plugin.ModVersion}, Remote: {action.Version}");
            RejectBehavior.SendAndDisconnect(action.SenderUid, RejectReason.ModVersionMismatch);
            return;
        }

        if (action.GameVersion != Plugin.GameVersion)
        {
            Plugin.Instance?.Log.LogError($"Game version mismatch! Local: {Plugin.GameVersion}, Remote: {action.GameVersion}");
            RejectBehavior.SendAndDisconnect(action.SenderUid, RejectReason.GameVersionMismatch);
            return;
        }

        if (action.PeerInfo?.IncrementalDataBase is not { IsIncrementalReady: true })
        {
            Plugin.Instance?.Log.LogWarning(
                $"Rejecting connection from '{action.PeerInfo?.PeerId}' (uid={action.SenderUid}): game resources not loaded");
            RejectBehavior.SendAndDisconnect(action.SenderUid, RejectReason.GameResourcesNotLoaded);
            return;
        }

        if (MpManager.LocalScene == Scene.IzakayaPrepScene || MpManager.LocalScene == Scene.WorkScene)
        {
            Plugin.Instance?.Log.LogWarning($"Rejecting connection from '{action.PeerInfo.PeerId}' (uid={action.SenderUid}): " +
                $"reconnection not allowed in {MpManager.LocalScene}");
            InGameConsole.ShowPassiveFromAnyThread(TextId.PrepWorkReconnectBlocked.Get(
                LiveModeManager.GetDisplayName(action.SenderUid, action.PeerInfo.PeerId)));
            RejectBehavior.SendAndDisconnect(action.SenderUid, RejectReason.PrepWorkReconnectBlocked, action.PeerInfo.PeerId);
            return;
        }

        if (MpManager.AllPlayersCount >= ConfigManager.MaxPlayers.Value)
        {
            Plugin.Instance?.Log.LogWarning($"Rejecting connection from '{action.PeerInfo.PeerId}' (uid={action.SenderUid}): " +
                $"room full ({MpManager.AllPlayersCount}/{ConfigManager.MaxPlayers.Value})");
            RejectBehavior.SendAndDisconnect(
                action.SenderUid,
                RejectReason.RoomFull,
                MpManager.AllPlayersCount.ToString(),
                ConfigManager.MaxPlayers.Value.ToString());
            InGameConsole.ShowPassiveFromAnyThread(TextId.RoomFullHostNotify.Get(
                LiveModeManager.GetDisplayName(action.SenderUid, action.PeerInfo.PeerId),
                MpManager.AllPlayersCount,
                ConfigManager.MaxPlayers.Value));
            return;
        }

        if (!MpManager.IsValidPlayerId(action.PeerInfo.PeerId))
        {
            Plugin.Instance?.Log.LogWarning(
                $"Rejecting connection (uid={action.SenderUid}): invalid PeerId '{action.PeerInfo.PeerId}'");
            RejectBehavior.SendAndDisconnect(action.SenderUid, RejectReason.InvalidPlayerId);
            return;
        }

        if (PlayerManager.IsPeerIdOnline(action.PeerInfo.PeerId))
        {
            Plugin.Instance?.Log.LogWarning($"Rejecting connection from '{action.PeerInfo.PeerId}' (uid={action.SenderUid}): " +
                "duplicate PeerId already online");
            RejectBehavior.SendAndDisconnect(action.SenderUid, RejectReason.DuplicatePeerId, action.PeerInfo.PeerId);
            InGameConsole.ShowPassiveFromAnyThread(TextId.DuplicatePeerIdHostNotify.Get(
                LiveModeManager.GetDisplayName(action.SenderUid, action.PeerInfo.PeerId)));
            return;
        }

        action.PeerInfo.Uid = action.SenderUid;
        var member = new RoomMember
        {
            Uid = action.SenderUid,
            PeerId = action.PeerInfo.PeerId,
            Role = WireRoomRole.Client,
            Scene = action.CurrentGameScene,
            Skin = action.PeerInfo.Skin,
            Resources = action.PeerInfo.IncrementalDataBase,
        };
        var peer = PlayerManager.UpsertRoomMember(member, MpConstants.DirectRoomId);

        if (MpManager.LocalScene is Scene.DayScene or Scene.WorkScene)
        {
            peer.ResetMotion();
            peer.SpawnForScene();
        }

        HelloAckBehavior.Send(action.PeerInfo.Uid);
        RoomAssignBehavior.SendDirect(action.PeerInfo.Uid);
        RoomAssignBehavior.BroadcastDirectExcept(action.PeerInfo.Uid);
        MpWire.OnPeerHandshakeComplete(action.PeerInfo.Uid);

        InGameConsole.ShowPassiveFromAnyThread(
            TextId.MpConnected.Get(LiveModeManager.GetDisplayName(action.PeerInfo.Uid)));
    }
}

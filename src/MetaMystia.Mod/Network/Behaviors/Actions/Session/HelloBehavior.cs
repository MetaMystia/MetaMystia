using System.Linq;
using Common.UI;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class HelloBehavior
{
    public static void Send()
    {
        new HelloAction
        {
            Player = PlayerManager.Local.ToFullData(),
            ModVersion = Plugin.ModVersion,
            GameVersion = Plugin.GameVersion,
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<HelloAction>(Handle,
            receiveScope: NetReceiveScope.EndpointOnly);
    }

    private static void Handle(HelloAction action)
    {
        if (action.ModVersion != Plugin.ModVersion)
        {
            Plugin.Instance?.Log.LogError($"Mod version mismatch! Local: {Plugin.ModVersion}, Remote: {action.ModVersion}");
            HandshakeRejectBehavior.SendAndDisconnect(action.SenderUid, HandshakeRejectReason.ModVersionMismatch);
            return;
        }

        if (action.GameVersion != Plugin.GameVersion)
        {
            Plugin.Instance?.Log.LogError($"Game version mismatch! Local: {Plugin.GameVersion}, Remote: {action.GameVersion}");
            HandshakeRejectBehavior.SendAndDisconnect(action.SenderUid, HandshakeRejectReason.GameVersionMismatch);
            return;
        }

        var player = action.Player;
        if (player?.Resources is not { IsIncrementalReady: true })
        {
            Plugin.Instance?.Log.LogWarning(
                $"Rejecting connection from '{player?.PeerId}' (uid={action.SenderUid}): game resources not loaded");
            HandshakeRejectBehavior.SendAndDisconnect(action.SenderUid, HandshakeRejectReason.GameResourcesNotLoaded);
            return;
        }

        if (MpManager.LocalScene == Scene.IzakayaPrepScene || MpManager.LocalScene == Scene.WorkScene)
        {
            Plugin.Instance?.Log.LogWarning($"Rejecting connection from '{player.PeerId}' (uid={action.SenderUid}): " +
                $"reconnection not allowed in {MpManager.LocalScene}");
            InGameConsole.ShowPassiveFromAnyThread(TextId.PrepWorkReconnectBlocked.Get());
            HandshakeRejectBehavior.SendAndDisconnect(action.SenderUid, HandshakeRejectReason.PrepWorkReconnectBlocked);
            return;
        }

        if (MpManager.OnlinePlayersCount >= ConfigManager.MaxPlayers.Value)
        {
            Plugin.Instance?.Log.LogWarning($"Rejecting connection from '{player.PeerId}' (uid={action.SenderUid}): " +
                $"room full ({MpManager.OnlinePlayersCount}/{ConfigManager.MaxPlayers.Value})");
            HandshakeRejectBehavior.SendAndDisconnect(
                action.SenderUid,
                HandshakeRejectReason.ServerFull);
            InGameConsole.ShowPassiveFromAnyThread(TextId.RoomFullHostNotify.Get(
                LiveModeManager.GetDisplayName(action.SenderUid, player.PeerId),
                MpManager.OnlinePlayersCount,
                ConfigManager.MaxPlayers.Value));
            return;
        }

        if (!MpManager.IsValidPlayerId(player.PeerId))
        {
            Plugin.Instance?.Log.LogWarning(
                $"Rejecting connection (uid={action.SenderUid}): invalid PeerId '{player.PeerId}'");
            HandshakeRejectBehavior.SendAndDisconnect(action.SenderUid, HandshakeRejectReason.InvalidPlayerId);
            return;
        }

        if (PlayerManager.IsPeerIdOnline(player.PeerId))
        {
            Plugin.Instance?.Log.LogWarning($"Rejecting connection from '{player.PeerId}' (uid={action.SenderUid}): " +
                "duplicate PeerId already online");
            HandshakeRejectBehavior.SendAndDisconnect(action.SenderUid, HandshakeRejectReason.DuplicatePlayerId);
            InGameConsole.ShowPassiveFromAnyThread(TextId.DuplicatePeerIdHostNotify.Get(
                LiveModeManager.GetDisplayName(action.SenderUid, player.PeerId)));
            return;
        }

        player.Uid = action.SenderUid;
        player.RoomId = MpConstants.DirectRoomId;
        player.Role = WireRoomRole.Client;
        var peer = PlayerManager.UpsertFullPlayer(player);

        if (MpManager.LocalScene == Scene.DayScene)
            PlayerManager.SpawnPeersForCurrentScene(new[] { peer });

        HelloAckBehavior.Send(player.Uid);

        var existing = new[] { PlayerManager.Local.ToFullData() }
            .Concat(PlayerManager.RoomPeers.Where(p => p.Uid != player.Uid).Select(p => p.ToFullData()))
            .ToArray();
        new RoomAssignAction
        {
            SenderUid = MpConstants.HostUid,
            Self = player,
            ExistingMembers = existing,
            WireTargetUid = player.Uid,
        }.Enqueue();
        foreach (var member in PlayerManager.RoomPeers.Where(p => p.Uid != player.Uid))
        {
            new RoomNewPlayerJoinedAction
            {
                SenderUid = MpConstants.HostUid,
                Joined = player,
                WireTargetUid = member.Uid,
            }.Enqueue();
        }

        InGameConsole.ShowPassiveFromAnyThread(
            TextId.MpConnected.Get(LiveModeManager.GetDisplayName(player.Uid)));
    }
}

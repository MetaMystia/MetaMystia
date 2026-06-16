using Common.UI;
using MetaMystia.Network.Services;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Messages.Session;
using MetaMystia.Protocol.Transport;
using MetaMystia.UI;

namespace MetaMystia.Network.Handlers;

[AutoLog]
public static partial class SessionHandlers
{
    public static void Register()
    {
        MessageDispatcher.Register<HelloMessage>(HandleHello);
        MessageDispatcher.Register<HelloAckMessage>(HandleHelloAck);
        MessageDispatcher.Register<RejectMessage>(HandleReject);
        MessageDispatcher.Register<PeerJoinMessage>(HandlePeerJoin);
        MessageDispatcher.Register<PeerLeaveMessage>(HandlePeerLeave);
        MessageDispatcher.Register<PlayerChangeIdMessage>(HandlePlayerChangeId);
        MessageDispatcher.Register<PingMessage>(HandlePing);
        MessageDispatcher.Register<PongMessage>(HandlePong);
    }

    public static void HandleHello(HelloMessage msg)
    {
        if (!MpManager.IsRoomHost)
        {
            Log.LogWarning("Hello received by non-host, ignoring");
            return;
        }

        var senderUid = msg.SenderUid;

        // --- 版本校验 ---
        if (msg.Version != Plugin.ModVersion)
        {
            Log.LogError($"Mod version mismatch! Local: {Plugin.ModVersion}, Remote: {msg.Version}");
            SessionServices.SendRejectAndDisconnect(senderUid, TextId.ModVersionMismatch);
            return;
        }

        if (msg.GameVersion != Plugin.GameVersion)
        {
            Log.LogError($"Game version mismatch! Local: {Plugin.GameVersion}, Remote: {msg.GameVersion}");
            SessionServices.SendRejectAndDisconnect(senderUid, TextId.GameVersionMismatch);
            return;
        }

        if (msg.PeerInfo.IncrementalDataBase.DLCFlags == Protocol.Enums.DLCPack.None)
        {
            Log.LogWarning($"Rejecting connection from '{msg.PeerInfo.PeerId}' (uid={senderUid}): game resources not loaded");
            SessionServices.SendRejectAndDisconnect(senderUid, TextId.GameResourcesNotLoaded);
            return;
        }

        // --- 备菜/营业阶段不允许重连 ---
        if (MpManager.LocalScene == Scene.IzakayaPrepScene || MpManager.LocalScene == Scene.WorkScene)
        {
            Log.LogWarning($"Rejecting connection from '{msg.PeerInfo.PeerId}' (uid={senderUid}): " +
                $"reconnection not allowed in {MpManager.LocalScene}");
            InGameConsole.ShowPassiveFromAnyThread(TextId.PrepWorkReconnectBlocked.Get(
                LiveModeManager.GetDisplayName(senderUid, msg.PeerInfo.PeerId)));
            SessionServices.SendRejectAndDisconnect(senderUid, TextId.PrepWorkReconnectBlocked, msg.PeerInfo.PeerId);
            return;
        }

        // --- 人数限制 ---
        if (MpManager.AllPlayersCount >= ConfigManager.MaxPlayers.Value)
        {
            Log.LogWarning($"Rejecting connection from '{msg.PeerInfo.PeerId}' (uid={senderUid}): " +
                $"room full ({MpManager.AllPlayersCount}/{ConfigManager.MaxPlayers.Value})");
            SessionServices.SendRejectAndDisconnect(senderUid,
                TextId.RoomFull, MpManager.AllPlayersCount.ToString(), ConfigManager.MaxPlayers.Value.ToString());
            InGameConsole.ShowPassiveFromAnyThread(TextId.RoomFullHostNotify.Get(
                LiveModeManager.GetDisplayName(senderUid, msg.PeerInfo.PeerId), MpManager.AllPlayersCount, ConfigManager.MaxPlayers.Value));
            return;
        }

        // --- PeerId 合法性校验 ---
        if (!MpManager.IsValidPlayerId(msg.PeerInfo.PeerId))
        {
            Log.LogWarning($"Rejecting connection (uid={senderUid}): invalid PeerId '{msg.PeerInfo.PeerId}'");
            SessionServices.SendRejectAndDisconnect(senderUid, TextId.MpPlayerIdInvalid);
            return;
        }

        // --- 重名检测 ---
        if (PlayerManager.IsPeerIdOnline(msg.PeerInfo.PeerId))
        {
            Log.LogWarning($"Rejecting connection from '{msg.PeerInfo.PeerId}' (uid={senderUid}): " +
                $"duplicate PeerId already online");
            SessionServices.SendRejectAndDisconnect(senderUid, TextId.DuplicatePeerId, msg.PeerInfo.PeerId);
            InGameConsole.ShowPassiveFromAnyThread(TextId.DuplicatePeerIdHostNotify.Get(
                LiveModeManager.GetDisplayName(senderUid, msg.PeerInfo.PeerId)));
            return;
        }

        // 注册新 peer
        msg.PeerInfo.Uid = senderUid;
        var peer = PlayerManager.AddPeer(msg.PeerInfo);

        // 如果主机当前在 DayScene，则为新加入的 peer 立即生成角色
        if (MpManager.LocalScene == Scene.DayScene)
        {
            peer.ResetMotion();
            peer.SpawnForScene();
        }

        // 向新客机发送 HelloAck（携带分配的 UID + 所有已有 peer 信息）
        SessionServices.SendHelloAck(senderUid);

        // 向所有已有客机广播新玩家加入
        SessionServices.BroadcastPeerJoin(senderUid, msg.PeerInfo);

        // 启动同步
        MpWire.OnPeerHandshakeComplete(senderUid);

        InGameConsole.ShowPassiveFromAnyThread(TextId.MpConnected.Get(LiveModeManager.GetDisplayName(senderUid)));
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleHelloAck(HelloAckMessage msg)
    {
        if (MpManager.IsRoomHost)
        {
            Log.LogWarning("HelloAck received by host, ignoring");
            return;
        }

        // 设置本地 UID
        PlayerManager.Local.Uid = msg.AssignedUid;
        Log.LogMessage($"Assigned UID: {msg.AssignedUid}");

        // 注册主机为 peer (uid=0)
        if (msg.HostInfo != null)
        {
            msg.HostInfo.Uid = MpConstants.HostUid;
            PlayerManager.AddPeer(msg.HostInfo);

            // 注册已有的其他 peer
            foreach (var existingPeerData in msg.ExistingPeers)
            {
                PlayerManager.AddPeer(existingPeerData);
            }

            // 如果当前在 DayScene（重连），立即为所有 peer 生成角色
            if (MpManager.LocalScene == Scene.DayScene)
            {
                PlayerManager.SpawnPeers();
            }

            MpWire.OnHandshakeComplete(msg.HostInfo.PeerId);
        }

        InGameConsole.ShowPassiveFromAnyThread(TextId.MpConnected.Get(LiveModeManager.GetDisplayName(MpConstants.HostUid)));
    }

    public static void HandleReject(RejectMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var reasonTextId = EnumConverter.ToGame(msg.Reason);

        // ReSharper disable once CoVariantArrayConversion
        var reason = reasonTextId.Get(msg.ReasonArgs);
        Log.LogWarning($"Connection rejected: {reason}");
        InGameConsole.ShowPassiveFromAnyThread(reason);
        MpWire.DisconnectPeer();
    }

    public static void HandlePeerJoin(PeerJoinMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        if (msg.PeerInfo.Uid == PlayerManager.Local.Uid) return;

        if (!PlayerManager.Peers.TryGetValue(msg.PeerInfo.Uid, out var peer))
        {
            peer = PlayerManager.AddPeer(msg.PeerInfo);

            // 如果当前在 DayScene，立即为新 peer 生成角色
            if (MpManager.LocalScene == Scene.DayScene)
            {
                peer.ResetMotion();
                peer.SpawnForScene();
            }
        }
        else
        {
            peer.IsDayOver = msg.PeerInfo.IsDayOver;
            peer.IsPrepOver = msg.PeerInfo.IsPrepOver;
        }

        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerJoined.Get(LiveModeManager.GetDisplayName(msg.PeerInfo.Uid)));
    }

    public static void HandlePeerLeave(PeerLeaveMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        if (!PlayerManager.Peers.TryGetValue(msg.PeerUid, out _)) return;
        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(msg.PeerUid)));
        PlayerManager.RemovePeer(msg.PeerUid);
    }

    public static void HandlePlayerChangeId(PlayerChangeIdMessage msg)
    {
        if (!PlayerManager.TryGetVisiblePeer(msg.SenderUid, out var peer)) return;
        var oldId = peer.Id;
        peer.Id = msg.NewPlayerId;
        var oldDisplay = LiveModeManager.IsActive ? LiveModeManager.FormatUid(msg.SenderUid) : oldId;
        var newDisplay = LiveModeManager.IsActive ? LiveModeManager.FormatUid(msg.SenderUid) : msg.NewPlayerId;
        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerPlayerIdChanged.Get(oldDisplay, newDisplay));
        FloatingTextHelper.UpdatePlayerLabel(msg.SenderUid, LiveModeManager.GetDisplayName(msg.SenderUid));
    }

    public static void HandlePing(PingMessage msg)
    {
        MpManager.TimeOffset = 0;
        SessionServices.SendPong(msg.Id);
    }

    public static void HandlePong(PongMessage msg)
    {
        MpWire.UpdateLatency(msg.Id);
    }
}

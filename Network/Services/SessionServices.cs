using System.Linq;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Data;
using MetaMystia.Protocol.Messages.Session;
using MetaMystia.Protocol.Transport;
using MetaMystia.UI;

namespace MetaMystia.Network.Services;

public static class SessionServices
{
    /// <summary>
    /// 客机发送 Hello 给主机请求连接
    /// </summary>
    public static void SendHello()
    {
        var peerInfoData = new PlayerInfoData
        {
            Uid = MpConstants.UnassignedUid,
            PeerId = MpManager.PlayerId,
            IncrementalDataBase = PlayerManager.Local.IncrementalDataBase?.ToDatabaseData() ?? new ResourceDatabaseData(),
            Skin = PlayerManager.Local.Skin,
            IsDayOver = PlayerManager.LocalIsDayOver,
            IsPrepOver = PlayerManager.LocalIsPrepOver
        };

        var msg = new HelloMessage
        {
            Version = Plugin.ModVersion,
            GameVersion = Plugin.GameVersion,
            CurrentGameScene = EnumConverter.ToProtocol(MpManager.LocalScene),
            PeerInfo = peerInfoData
        };
        MpWire.Send(msg);
    }

    /// <summary>
    /// 主机向指定客机发送 HelloAck（携带分配的 UID + 所有已有 peer 信息）
    /// </summary>
    public static void SendHelloAck(int clientUid)
    {
        if (!MpManager.IsRoomHost) return;

        var existingPeers = PlayerManager.Peers
            .Where(kvp => kvp.Key != clientUid)
            .Select(kvp => kvp.Value.ToPlayerInfoData())
            .ToArray();

        var hostInfoData = PlayerManager.Local.ToPlayerInfoData();

        var msg = new HelloAckMessage
        {
            AssignedUid = clientUid,
            HostInfo = hostInfoData,
            ExistingPeers = existingPeers
        };
        MpWire.Send(msg);
    }

    /// <summary>
    /// 主机向指定客机发送拒绝消息并断开连接
    /// </summary>
    public static void SendRejectAndDisconnect(int uid, TextId reasonId, params string[] args)
    {
        if (!MpManager.IsRoomHost) return;

        var msg = new RejectMessage
        {
            Reason = EnumConverter.ToProtocol(reasonId),
            ReasonArgs = args
        };
        MpWire.Send(msg);
        MpWire.DisconnectClient(uid);
    }

    /// <summary>
    /// 主机向除新玩家以外的所有客机广播新玩家加入
    /// </summary>
    public static void BroadcastPeerJoin(int newPeerUid, PlayerInfoData peerInfo)
    {
        if (!MpManager.IsRoomHost) return;
        if (PlayerManager.Peers.Count <= 1) return;

        var msg = new PeerJoinMessage { PeerInfo = peerInfo };
        MpWire.Send(msg);
    }

    /// <summary>
    /// 主机向所有客机广播玩家离开
    /// </summary>
    public static void BroadcastPeerLeave(int leavingUid)
    {
        if (!MpManager.IsRoomHost) return;

        var msg = new PeerLeaveMessage { PeerUid = leavingUid };
        MpWire.Send(msg);
    }

    /// <summary>
    /// 发送玩家 ID 变更通告（任何玩家 → 所有玩家）
    /// </summary>
    public static void SendPlayerChangeId(string newId)
    {
        // 更新本地玩家自己的头顶标签
        PlayerManager.Local.Id = newId;
        FloatingTextHelper.UpdatePlayerLabel(PlayerManager.Local.Uid, LiveModeManager.GetDisplayName(PlayerManager.Local.Uid));

        var msg = new PlayerChangeIdMessage { NewPlayerId = newId };
        MpWire.Send(msg);
    }

    /// <summary>
    /// 发送 Ping（用于延迟测量）
    /// </summary>
    public static void SendPing(int id)
    {
        var msg = new PingMessage { Id = id };
        MpWire.Send(msg);
    }

    /// <summary>
    /// 发送 Pong（回复 Ping）
    /// </summary>
    public static void SendPong(int id)
    {
        var msg = new PongMessage { Id = id };
        MpWire.Send(msg);
    }
}

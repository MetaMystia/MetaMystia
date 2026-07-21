using MemoryPack;

using Common.UI;

using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>
/// 客机 → 主机：握手请求。主机验证后回复 HelloAckAction。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class HelloAction : Action
{
    public string Version { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public Scene CurrentGameScene { get; set; }
    public PlayerInfo PeerInfo { get; set; }

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Message;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Message;
    public new void LogActionSend() => base.LogActionSend();

    /// <summary>
    /// 仅主机处理：验证客机，分配 UID，回复 HelloAck，通告已有客机
    /// </summary>
    [HostOnlyReceive]
    public override void OnReceivedDerived()
    {
        // --- 版本校验 ---
        if (Version != Plugin.ModVersion)
        {
            Log.LogError($"Mod version mismatch! Local: {Plugin.ModVersion}, Remote: {Version}");
            RejectAction.SendAndDisconnect(SenderUid, TextId.ModVersionMismatch);
            return;
        }

        if (GameVersion != Plugin.GameVersion)
        {
            Log.LogError($"Game version mismatch! Local: {Plugin.GameVersion}, Remote: {GameVersion}");
            RejectAction.SendAndDisconnect(SenderUid, TextId.GameVersionMismatch);
            return;
        }

        if (PeerInfo?.IncrementalDataBase is not { IsIncrementalReady: true })
        {
            Log.LogWarning($"Rejecting connection from '{PeerInfo?.PeerId}' (uid={SenderUid}): game resources not loaded");
            RejectAction.SendAndDisconnect(SenderUid, TextId.GameResourcesNotLoaded);
            return;
        }

        // --- 备菜/营业阶段不允许重连 ---
        if (MpManager.LocalScene == Scene.IzakayaPrepScene || MpManager.LocalScene == Scene.WorkScene)
        {
            Log.LogWarning($"Rejecting connection from '{PeerInfo.PeerId}' (uid={SenderUid}): " +
                $"reconnection not allowed in {MpManager.LocalScene}");
            InGameConsole.ShowPassiveFromAnyThread(TextId.PrepWorkReconnectBlocked.Get(
                LiveModeManager.GetDisplayName(SenderUid, PeerInfo.PeerId)));
            RejectAction.SendAndDisconnect(SenderUid, TextId.PrepWorkReconnectBlocked, PeerInfo.PeerId);
            return;
        }

        // --- 人数限制 ---
        if (MpManager.AllPlayersCount >= ConfigManager.MaxPlayers.Value)
        {
            Log.LogWarning($"Rejecting connection from '{PeerInfo.PeerId}' (uid={SenderUid}): " +
                $"room full ({MpManager.AllPlayersCount}/{ConfigManager.MaxPlayers.Value})");
            RejectAction.SendAndDisconnect(SenderUid,
                TextId.RoomFull, MpManager.AllPlayersCount.ToString(), ConfigManager.MaxPlayers.Value.ToString());
            InGameConsole.ShowPassiveFromAnyThread(TextId.RoomFullHostNotify.Get(
                LiveModeManager.GetDisplayName(SenderUid, PeerInfo.PeerId), MpManager.AllPlayersCount, ConfigManager.MaxPlayers.Value));
            return;
        }

        // --- PeerId 合法性校验 ---
        if (!MpManager.IsValidPlayerId(PeerInfo.PeerId))
        {
            Log.LogWarning($"Rejecting connection (uid={SenderUid}): invalid PeerId '{PeerInfo.PeerId}'");
            RejectAction.SendAndDisconnect(SenderUid, TextId.MpPlayerIdInvalid);
            return;
        }

        // --- 重名检测 ---
        if (PlayerManager.IsPeerIdOnline(PeerInfo.PeerId))
        {
            Log.LogWarning($"Rejecting connection from '{PeerInfo.PeerId}' (uid={SenderUid}): " +
                $"duplicate PeerId already online");
            RejectAction.SendAndDisconnect(SenderUid, TextId.DuplicatePeerId, PeerInfo.PeerId);
            InGameConsole.ShowPassiveFromAnyThread(TextId.DuplicatePeerIdHostNotify.Get(
                LiveModeManager.GetDisplayName(SenderUid, PeerInfo.PeerId)));
            return;
        }

        PeerInfo.Uid = SenderUid;
        var peer = PlayerManager.AddPeer(PeerInfo);

        // 如果主机当前在 DayScene，则为新加入的 peer 立即生成角色
        if (MpManager.LocalScene == Scene.DayScene)
        {
            peer.ResetMotion();
            peer.SpawnForScene();
        }

        // 向新客机发送 HelloAck（携带分配的 UID + 所有已有 peer 信息）
        HelloAckAction.Send(PeerInfo.Uid);

        // 向所有已有客机通告新玩家加入
        PeerJoinAction.Send(PeerInfo.Uid, PeerInfo);

        // 启动同步
        MpWire.OnPeerHandshakeComplete(PeerInfo.Uid);

        InGameConsole.ShowPassiveFromAnyThread(TextId.MpConnected.Get(LiveModeManager.GetDisplayName(PeerInfo.Uid)));
    }

    /// <summary>
    /// 客机发送 Hello 给主机请求连接
    /// </summary>
    public static void Send()
    {
        PlayerInfo peerInfo = new PlayerInfo()
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
            CurrentGameScene = MpManager.LocalScene,
            GameVersion = Plugin.GameVersion,
        }.Enqueue();
    }
}

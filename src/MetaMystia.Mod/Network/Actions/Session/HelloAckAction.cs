using MemoryPack;
using System.Linq;
using Il2CppSystem.Net.Http.Headers;
using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 客机：握手确认，携带分配的 UID 和现有所有 peer 信息
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class HelloAckAction : Action
{
    public int AssignedUid { get; set; }

    /// <summary>
    /// 主机信息（Uid = Session.HostUid）
    /// </summary>
    public PlayerInfo HostInfo { get; set; }

    /// <summary>
    /// 已有 peer 列表（不含新加入者自身和主机）
    /// </summary>
    public PlayerInfo[] ExistingPeers { get; set; } = [];

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Message;

    protected override string ToLogString()
    {
        var existingPeers = ExistingPeers ?? [];
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            AssignedUid,
            HostPeerId = HostInfo?.PeerId,
            ExistingPeersCount = existingPeers.Length,
            ExistingPeerIds = existingPeers.Take(3).Select(peer => peer.PeerId).ToArray(),
            ExistingPeersTruncated = existingPeers.Length > 3
        });
    }

    /// <summary>
    /// 客机处理：设置自身 UID，注册主机和已有 peer
    /// </summary>
    [ClientOnlyReceive]
    public override void OnReceivedDerived()
    {
        // 设置本地 UID
        PlayerManager.Local.Uid = AssignedUid;
        Log.LogMessage($"Assigned UID: {AssignedUid}");

        // 记录主机下发的真实 HostUid（直连模式下为 0，服务端模式下由服务端分配）
        MpWire.Session.AssignHostUid(HostInfo.Uid);

        // 注册主机为 peer
        PlayerManager.AddPeer(HostInfo);

        // 注册已有的其他 peer
        foreach (var peerInfo in ExistingPeers)
        {
            PlayerManager.AddPeer(peerInfo);
        }

        // 如果当前在 DayScene（重连），立即为所有 peer 生成角色
        if (MpManager.LocalScene == Common.UI.Scene.DayScene)
        {
            PlayerManager.SpawnPeers();
        }

        MpWire.OnHandshakeComplete(HostInfo.PeerId);
        InGameConsole.ShowPassiveFromAnyThread(TextId.MpConnected.Get(LiveModeManager.GetDisplayName(HostInfo.Uid)));
    }

    /// <summary>
    /// 主机向指定客机发送 HelloAck
    /// </summary>
    public static void Send(int clientUid)
    {
        if (!MpManager.IsRoomHost) return;

        var existingPeers = new System.Collections.Generic.List<PlayerInfo>();
        foreach (var kvp in PlayerManager.Peers)
        {
            if (kvp.Key == clientUid) continue;
            existingPeers.Add(PlayerInfo.FromPlayer(kvp.Value));
        }

        new HelloAckAction
        {
            AssignedUid = clientUid,
            HostInfo = PlayerInfo.FromPlayer(PlayerManager.Local),
            ExistingPeers = existingPeers.ToArray(),
            WireTargetUid = clientUid,
        }.Enqueue();
    }
}

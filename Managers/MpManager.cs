using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MetaMystia.Network;
using MetaMystia.Patch;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

[AutoLog]
public static partial class MpManager
{
    public enum ROLE
    {
        Host,
        Client
    }

    #region Const Values
    public const int DEFAULT_PORT = 40815;
    public const int HOST_UID = 0;
    public const int UNASSIGNED_UID = -1;
    private const string SyncActionCommandID = "SyncAction";
    #endregion

    public static int ConfigPort => ConfigManager.DefaultPort?.Value ?? DEFAULT_PORT;
    public static int CurrentPort { get; private set; } = DEFAULT_PORT;
    public static bool EnableIPv6 => ConfigManager.EnableIPv6?.Value ?? false;

    /// <summary>
    /// 校验玩家 ID 是否合法：不能为空，不能包含空格、尖括号或控制字符
    /// </summary>
    public static bool IsValidPlayerId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        foreach (char c in id)
        {
            if (c == '<' || c == '>' || char.IsWhiteSpace(c) || char.IsControl(c))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 清理不合法字符，返回合法 ID；若清理后为空则返回 fallback
    /// </summary>
    public static string SanitizePlayerId(string id, string fallback = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return fallback ?? Environment.MachineName;

        var sb = new StringBuilder();
        foreach (char c in id)
        {
            if (c != '<' && c != '>' && !char.IsWhiteSpace(c) && !char.IsControl(c))
                sb.Append(c);
        }
        var result = sb.ToString();
        return string.IsNullOrEmpty(result) ? (fallback ?? Environment.MachineName) : result;
    }

    #region Multiplayer Related Values
    public static string PlayerId { get => ConfigManager.GetPlayerId(); set => ConfigManager.SetPlayerId(value); }
    public static long Latency { get; private set; } = 0;

    public static int ConnectedPlayersCount => PlayerManager.Peers.Count;
    public static int AllPlayersCount => ConnectedPlayersCount + 1;
    #endregion

    private static TcpServer server = null;
    private static TcpClientWrapper client = null;
    private static ROLE Role;
    public static bool IsRunning { get; private set; }
    public static bool IsHost => Role == ROLE.Host;
    public static bool IsClient => Role == ROLE.Client;
    public static bool IsConnecting { get; private set; } = false;
    public static bool IsConnected => (IsHost ? server?.HasAnyClient : client?.IsConnected) ?? false;
    public static bool IsConnectedClient => IsConnected && IsClient;
    public static bool IsConnectedHost => IsConnected && IsHost;
    public static string RoleTag => IsHost ? "[S]" : "[C]";
    public static string RoleName => IsHost ? "Host" : "Client";

    private static ConcurrentDictionary<int, long> pingSendTimes = new();
    public static long TimestampNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static long TimeOffset = 0;
    public static long GetSynchronizedTimestampNow => TimestampNow - TimeOffset;
    private static int _pingId = 0;

    #region SinglePlay GamePlay Getters
    public static Common.UI.Scene LocalScene { get; private set; } = Common.UI.Scene.EmptyScene;
    public static Common.UI.Scene PeerScene = Common.UI.Scene.EmptyScene;
    #endregion


    #region 剧情相关
    private static bool _inStory;

    public static bool InStory => _inStory;

    public static void RefreshInStoryCache()
    {
        var director = Common.SceneDirector.Instance?.playableDirector;
        _inStory = director != null &&
            (director.state == UnityEngine.Playables.PlayState.Playing
             || director.state == UnityEngine.Playables.PlayState.Delayed);
    }

    public static bool ShouldSkipAction => !IsConnected || InStory;
    #endregion

    public const string PeerGetCharacterUnitNotNullCommand = "PeerGetCharacterUnitNotNullCommand";

    public static int WorkTimeSecondOverride = 9 * 60;

    public static void SwitchRole(bool stop_existed_server = true)
    {
        Log.Message($"Switching role from {Role} to {(IsHost ? "Client" : "Host")}");
        if (IsHost)
        {
            if (stop_existed_server)
            {
                server?.Stop();
                server = null;
            }
            Role = ROLE.Client;
        }
        else
        {
            client?.Close();
            server = new(CurrentPort, EnableIPv6);
            server.Start();
            Role = ROLE.Host;
        }
    }

    public static bool Start(ROLE r = ROLE.Host, int port = -1)
    {
        if (port == -1) port = ConfigPort;
        Log.Info($"{DebugText}");
        if (!Plugin.AllPatched)
        {
            InGameConsole.LogToConsole($"<color=#FF6666>Cannot start multiplayer, patch failure!\n{DebugText}</color>");
            Log.Fatal($"Cannot start multiplayer, patch failure!\n{DebugText}");
            return false;
        }

        if (IsRunning)
        {
            Log.LogWarning("MpManager is already running");
            return true;
        }

        IsRunning = true;
        Role = r;
        CurrentPort = port;
        PlayerManager.Local.Id = PlayerId;

        switch (r)
        {
            case ROLE.Host:
                PlayerManager.Local.Uid = HOST_UID;
                server = new(CurrentPort, EnableIPv6);
                server.Start();
                Log.LogInfo($"Starting MpManager as host on port {CurrentPort}");
                break;
            case ROLE.Client:
                PlayerManager.Local.Uid = UNASSIGNED_UID;
                Log.LogInfo("Starting MpManager as client");
                break;
        }
        return true;
    }

    public static void Stop()
    {
        if (!IsRunning)
            return;

        Log.LogInfo("Stopping MpManager");
        IsRunning = false;

        try
        {
            server?.Stop();
            server = null;
            client?.Close();
            client = null;
        }
        catch (Exception e)
        {
            Log.LogError($"Error stopping: {e.Message}");
        }
        Log.LogInfo("MpManager has stopped");
    }

    public static bool Restart()
    {
        var port = CurrentPort;
        Stop();
        return Start(Role, port);
    }

    public static async Task<bool> ConnectToPeerAsync(string peerIp, int port = -1, bool stop_existed_server = true)
    {
        if (port == -1) port = ConfigPort;
        if (!IsRunning && !Start(ROLE.Client))
        {
            return false;
        }

        if (IsConnected)
        {
            Log.LogWarning("[C] Already connected to a peer. Please disconnect first.");
            return false;
        }

        if (IsConnecting)
        {
            Log.LogWarning("[C] Now try connecting to a peer, please wait..");
            return false;
        }

        try
        {
            IsConnecting = true;
            if (IsHost)
            {
                SwitchRole(stop_existed_server);
            }
            InGameConsole.LogToConsole(TextId.MpConnecting.Get(peerIp, port));
            Log.LogInfo($"[C] Connecting to {peerIp}:{port}...");
            client = new(peerIp, port);
            await client.StartAsync();
            OnClientConnected(client.GetRealConnectedIp);
            Log.LogMessage($"[C] Successfully connected to peer {peerIp}:{port}");

            return true;
        }
        catch (Exception e)
        {
            Log.LogError($"[C] Error connecting to peer: {e.Message}");
            return false;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>
    /// 客机 TCP 连接建立后调用：发送 Hello 包给主机
    /// </summary>
    public static void OnClientConnected(string ip)
    {
        HelloAction.Send();
    }

    /// <summary>
    /// 客机收到 HelloAck 后调用：握手完成，启动同步
    /// </summary>
    public static void OnHandshakeComplete(string hostId)
    {
        SceneTransitAction.Send(LocalScene);
        CommandScheduler.EnqueueInterval(SyncActionCommandID, 0.5f, SyncAction.Send);
        InGameConsole.ShowPassiveFromAnyThread(TextId.MultiplayerConnected.Get());
    }

    /// <summary>
    /// 主机对新客机握手完成后调用
    /// </summary>
    public static void OnPeerHandshakeComplete(int uid)
    {
        CommandScheduler.EnqueueInterval(SyncActionCommandID, 2f, SyncAction.Send);
    }

    /// <summary>
    /// 客机断开连接时调用
    /// </summary>
    public static void OnDisconnected()
    {
        PlayerManager.ClearPeers();
        PlayerManager.Local.Uid = UNASSIGNED_UID;
        CommandScheduler.RemoveKeyFromKeyQueue(PeerGetCharacterUnitNotNullCommand);
        CommandScheduler.CancelInterval(SyncActionCommandID);
        InGameConsole.ShowPassiveFromAnyThread(TextId.MultiplayerDisconnected.Get());
    }

    /// <summary>
    /// 主机侧：某个客机断开连接时调用
    /// </summary>
    public static void OnClientDisconnected(int uid)
    {
        string peerId = null;
        if (PlayerManager.Peers.TryGetValue(uid, out var peer))
        {
            peerId = peer.Id;
            Log.LogMessage($"Client uid={uid} (id='{peerId}') disconnected");
            InGameConsole.ShowPassiveFromAnyThread(TextId.PeerLeft.Get(peerId));
            Network.PeerLeaveAction.BroadcastPeerLeave(uid);
            PlayerManager.RemovePeer(uid);
        }
        if (PlayerManager.Peers.IsEmpty)
        {
            CommandScheduler.CancelInterval(SyncActionCommandID);
        }
        // 检查是否可以继续流程
        CheckContinueAfterDisconnect(uid, peerId);
    }

    /// <summary>
    /// 主机侧：peer 断开后检查是否剩余玩家均已就绪，提示用户可以继续
    /// </summary>
    private static void CheckContinueAfterDisconnect(int disconnectedUid, string disconnectedName)
    {
        if (!IsHost) return;
        disconnectedName ??= $"uid={disconnectedUid}";

        // 没有剩余 peer 时不提示 continue（单人模式不需要）
        bool hasPeers = !PlayerManager.Peers.IsEmpty;

        switch (LocalScene)
        {
            case Common.UI.Scene.DayScene:
                // 检查是否在等待 DayOver
                if (PlayerManager.LocalIsDayOver)
                {
                    if (!hasPeers || PlayerManager.AllPeersDayOver)
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerDisconnectedAllReady.Get(
                            disconnectedName, "/mp continue day"));
                    }
                    else
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerDisconnectedWaiting.Get(
                            disconnectedName));
                    }
                }
                break;
            case Common.UI.Scene.IzakayaPrepScene:
                // 检查是否在等待 PrepOver
                if (PlayerManager.LocalIsPrepOver)
                {
                    if (!hasPeers || PlayerManager.AllPeersPrepOver)
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerDisconnectedAllReady.Get(
                            disconnectedName, "/mp continue prep"));
                    }
                    else
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerDisconnectedWaiting.Get(
                            disconnectedName));
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 主机侧：收到客机发来的 Action。主机先处理，如果 Action 标记了 HostRelay 则转发。
    /// 使用 zero-copy relay：直接在原始字节上修改 SenderUid 并广播，跳过 reserialize。
    /// </summary>
    public static void OnActionFromClient(Network.Action action, int clientUid, byte[] rawBody)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            action.SenderUid = clientUid;
            action.OnReceived();

            if (action.GetType().GetCustomAttributes(typeof(Network.Action.HostRelayAttribute), false).Length > 0)
            {
                BitConverter.GetBytes(clientUid).CopyTo(rawBody, Network.RelayConstants.SenderUidOffset);
                byte[] framed = new byte[4 + rawBody.Length];
                BitConverter.GetBytes(rawBody.Length).CopyTo(framed, 0);
                Buffer.BlockCopy(rawBody, 0, framed, 4, rawBody.Length);
                server?.SendRawToExcept(clientUid, framed);
            }
        });
    }

    /// <summary>
    /// 客机侧：收到主机发来的 Action
    /// </summary>
    public static void OnAction(Network.Action action)
    {
        PluginManager.Instance.RunOnMainThread(() => action.OnReceived());
    }

    #region 发送方法

    /// <summary>
    /// 客机→主机，或主机→所有客机广播
    /// </summary>
    public static void SendToHostOrBroadcast(NetPacket packet)
    {
        if (IsHost)
        {
            server?.Broadcast(packet);
        }
        else
        {
            client?.Send(packet);
        }
    }

    /// <summary>
    /// 低优先级发送：拥塞时丢弃（用于位置同步等高频包）
    /// </summary>
    public static void SendToHostOrBroadcastLowPriority(NetPacket packet)
    {
        if (IsHost)
        {
            server?.BroadcastLowPriority(packet);
        }
        else
        {
            client?.SendLowPriority(packet);
        }
    }

    /// <summary>
    /// 客机→主机
    /// </summary>
    public static void SendToHost(NetPacket packet)
    {
        if (!IsClient) return;
        client?.Send(packet);
    }

    /// <summary>
    /// 主机→指定客机
    /// </summary>
    public static void SendToClient(int uid, NetPacket packet)
    {
        if (!IsHost) return;
        server?.SendTo(uid, packet);
    }

    /// <summary>
    /// 主机→除了 exceptUid 以外的所有客机
    /// </summary>
    public static void SendToAllExcept(int exceptUid, NetPacket packet)
    {
        if (!IsHost) return;
        server?.SendToExcept(exceptUid, packet);
    }

    #endregion

    public static void DisconnectPeer()
    {
        if (IsConnected)
        {
            if (IsHost)
            {
                server.DisconnectAllClients();
                PlayerManager.ClearPeers();
                CommandScheduler.RemoveKeyFromKeyQueue(PeerGetCharacterUnitNotNullCommand);
                CommandScheduler.CancelInterval(SyncActionCommandID);
            }
            else
            {
                client.Close(); // triggers OnDisconnected() internally
            }
            Log.LogMessage("All peer connections disconnected");
        }
    }

    /// <summary>
    /// 主机断开指定客机
    /// </summary>
    public static void DisconnectClient(int uid)
    {
        if (!IsHost) return;
        server?.DisconnectClient(uid);
        // 清理幽灵 Peer (Socket 意外断开而 PlayerManager 中残留 Peer)
        if (PlayerManager.Peers.ContainsKey(uid))
        {
            OnClientDisconnected(uid);
        }
    }

    /// <summary>
    /// Peer A -> Ping -> Peer B
    /// Peer A <- Pong <- Peer B
    /// Peer A calculates latency
    /// </summary>
    public static void SendPing()
    {
        if (!IsConnected) return;
        var t = TimestampNow;
        int id = _pingId++;
        pingSendTimes[id] = t;
        if (IsHost)
        {
            // 主机向所有客机发 Ping
            var action = new PingAction { Id = id };
            var packet = NetPacket.FromSingleAction(action);
            server?.Broadcast(packet);
        }
        else
        {
            PingAction.SendPing(id);
        }
    }

    public static void UpdateLatency(int id)
    {
        if (!pingSendTimes.TryRemove(id, out long t)) return;
        Latency = (TimestampNow - t) / 2;
    }

    public static string GetStatus()
    {
        StringBuilder status = new();
        status.AppendLine($"Self: {RoleTag} {PlayerId} (uid={PlayerManager.Local.Uid})");
        status.AppendLine($"Port: {CurrentPort} | Running: {(IsRunning ? "Yes" : "No")} | Connected: {(IsConnected ? "Yes" : "No")}");
        if (IsConnected)
        {
            status.AppendLine($"Ping: {Latency} ms | Players: {AllPlayersCount}");
            foreach (var kvp in PlayerManager.Peers)
            {
                var role = kvp.Key == HOST_UID ? "[S]" : "[C]";
                status.AppendLine($"  Peer: {role} {kvp.Value.Id} (uid={kvp.Key})");
            }
        }
        return status.ToString();
    }

    public static string BriefStatus
    {
        get
        {
            if (!Plugin.AllPatched)
            {
                return $"{TextId.ModPatchFailure.Get()} {BriefDebugText}";
            }
            if (!IsRunning)
            {
                return "Multiplayer: Off";
            }
            if (IsConnected)
            {
                var peerNames = string.Join(", ", PlayerManager.Peers.Values.Select(p => p.Id));
                return $"MP: {RoleTag} uid={PlayerManager.Local.Uid} | {AllPlayersCount}Players | ping {Latency}ms | {peerNames}";
            }
            else
            {
                return $"MP: {RoleName} (not connected)";
            }
        }
    }

    public static string DebugText
    {
        get
        {
            StringBuilder sb = new();
            sb.AppendLine($"{BriefDebugText}");
            sb.AppendLine($"{BriefStatus}");
            return sb.ToString();
        }
    }

    private static string BriefDebugText =>
        $"{Plugin.GameVersion}: {Plugin.ModVersion}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}, {DateTimeOffset.Now}";

    public static void OnSceneTransit(Common.UI.Scene newScene)
    {
        Log.Message($"LocalScene transit from {LocalScene} -> {newScene}");
        SceneTransitAction.Send(newScene);
        LocalScene = newScene;
        if (newScene == Common.UI.Scene.MainScene)
        {
            if (IsConnected)
            {
                Log.Message($"Transit to {newScene}, disconnecting peers");
                DisconnectPeer();
            }
            else if (!PlayerManager.Peers.IsEmpty)
            {
                Log.Message($"Transit to {newScene}, clearing stale peers");
                PlayerManager.ClearPeers();
                CommandScheduler.RemoveKeyFromKeyQueue(PeerGetCharacterUnitNotNullCommand);
                CommandScheduler.CancelInterval(SyncActionCommandID);
            }
        }
    }

    public static void DayOver()
    {
        if (!IsConnectedHost) return;
        Log.Message($"DayOver check: AllDayOver={PlayerManager.AllDayOver}");
        if (PlayerManager.AllDayOver)
        {
            ReadyAction.Broadcast(ReadyType.DayOver);

            // For host who will not receive DayOver allready
            CommandScheduler.EnqueueWithNoCondition(() =>
            {
                InGameConsole.ShowPassive(TextId.AllReadyTransition.Get());
                DaySceneManagerPatch.OnDayOver();
            });
        }
    }

    public static void PrepOver()
    {
        if (!IsConnectedHost) return;
        Log.Message($"PrepOver check: AllPrepOver={PlayerManager.AllPrepOver}");

        if (PlayerManager.AllPrepOver)
        {
            ReadyAction.Broadcast(ReadyType.PrepOver);

            // For host who will not receive PrepOver allready
            CommandScheduler.EnqueueWithNoCondition(IzakayaConfigPannelPatch.PrepOver);
        }
    }

    /// <summary>
    /// 主机强制继续 DayOver 流程（跳过已断开的 peer 的等待）
    /// </summary>
    /// <returns>是否成功执行</returns>
    public static bool ContinueDay()
    {
        if (!IsHost)
        {
            Log.LogWarning("ContinueDay: only host can execute");
            return false;
        }
        if (LocalScene != Common.UI.Scene.DayScene)
        {
            Log.LogWarning($"ContinueDay: not in DayScene (current: {LocalScene})");
            return false;
        }
        if (!PlayerManager.LocalIsDayOver)
        {
            Log.LogWarning("ContinueDay: local player has not DayOver'd yet");
            return false;
        }

        Log.Message("ContinueDay: forcing DayOver for all remaining players");

        // 将所有在线 peer 标记为 DayOver（防止还有人没标记）
        foreach (var peer in PlayerManager.Peers.Values)
            peer.IsDayOver = true;

        ReadyAction.Broadcast(ReadyType.DayOver);
        CommandScheduler.EnqueueWithNoCondition(() =>
        {
            InGameConsole.ShowPassive(TextId.AllReadyTransition.Get());
            DaySceneManagerPatch.OnDayOver();
        });
        return true;
    }

    /// <summary>
    /// 主机强制继续 PrepOver 流程（跳过已断开的 peer 的等待）
    /// </summary>
    /// <returns>是否成功执行</returns>
    public static bool ContinuePrep()
    {
        if (!IsHost)
        {
            Log.LogWarning("ContinuePrep: only host can execute");
            return false;
        }
        if (LocalScene != Common.UI.Scene.IzakayaPrepScene && LocalScene != Common.UI.Scene.WorkScene)
        {
            Log.LogWarning($"ContinuePrep: not in PrepScene (current: {LocalScene})");
            return false;
        }
        if (!PlayerManager.LocalIsPrepOver)
        {
            Log.LogWarning("ContinuePrep: local player has not PrepOver'd yet");
            return false;
        }

        Log.Message("ContinuePrep: forcing PrepOver for all remaining players");

        foreach (var peer in PlayerManager.Peers.Values)
            peer.IsPrepOver = true;

        ReadyAction.Broadcast(ReadyType.PrepOver);
        CommandScheduler.EnqueueWithNoCondition(IzakayaConfigPannelPatch.PrepOver);
        return true;
    }
}

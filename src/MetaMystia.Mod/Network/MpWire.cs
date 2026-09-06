using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia.Network;

/// <summary>线层：单 IO 线程收发，应用层经 <see cref="EnqueueSend"/> 统一出站。</summary>
[AutoLog]
public static partial class MpWire
{
    public const string SyncActionCommandId = "SyncAction";

    public static MpSession Session { get; } = new();

    private static DirectTcp _tcp;
    private static Thread _ioThread;
    private static volatile bool _ioRunning;
    private static volatile bool _running;
    private static volatile bool _connecting;
    private enum ClientHandshakeStage { None, AwaitingInfo, AwaitingHelloAck, Complete }
    private static volatile ClientHandshakeStage _clientHandshake;
    private static readonly ConcurrentDictionary<int, PeerHandshake> _peerHandshakes = new();
    private sealed record PeerHandshake(ConnectionInfoAction Info, bool Complete = false);

    private static readonly ConcurrentQueue<Outbound> _outbox = new();
    private static readonly ConcurrentQueue<Inbound> _inbox = new();
    private static readonly ConcurrentDictionary<int, long> _pingSent = new();

    private static int _pingId;
    private static long _lastPingMs;
    private static int _currentPort = MpConstants.DefaultPort;

    private const int PingIntervalMs = 3000;
    private const int ConnectTimeoutMs = 10_000;

    private readonly record struct Outbound(byte[] Framed, int? TargetUid, int? ExceptUid, bool LowPriority, bool CompletesHandshake = false, bool CloseAfterSend = false);
    // Action 为 null 表示断开，与已收包共用队列以保持先后顺序。
    private readonly record struct Inbound(int FromUid, Action Action);

    public static int CurrentPort => _currentPort;
    public static bool IsRunning => _running;
    public static bool IsConnecting => _connecting || _clientHandshake is ClientHandshakeStage.AwaitingInfo or ClientHandshakeStage.AwaitingHelloAck;
    public static bool IsAwaitingHelloAck => _clientHandshake == ClientHandshakeStage.AwaitingHelloAck;
    public static ConnectionInfoAction RemoteConnectionInfo { get; private set; }
    public static long LatencyMs { get; private set; }
    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static long TimeOffsetMs { get; set; }
    public static long SyncedNowMs => NowMs - TimeOffsetMs;

    private static bool HasTransportConnection => Session.TransportKind switch
    {
        TransportKind.DirectHost => _tcp?.HasClients == true,
        TransportKind.DirectClient => _tcp?.IsClientConnected == true,
        _ => false
    };

    public static bool IsRoomConnected => HasTransportConnection && (Session.IsRoomHost
        ? _peerHandshakes.Any(peer => peer.Value.Complete)
        : _clientHandshake == ClientHandshakeStage.Complete);

    public static bool CanSend => Session.TransportKind switch
    {
        TransportKind.DirectHost => Session.IsInRoom && IsRoomConnected,
        TransportKind.DirectClient => Session.IsInRoom && IsRoomConnected,
        TransportKind.RelayClient => Session.IsInPublicScope || Session.IsInRoom,
        _ => false
    };

    internal static bool CanSendAction(Action action)
    {
        if (CanSend) return true;
        if (!HasTransportConnection) return false;
        if (Session.IsRoomHost)
            return action is ConnectionInfoAction or HelloAckAction or RejectAction;
        return (_clientHandshake == ClientHandshakeStage.AwaitingInfo && action is ConnectionInfoAction)
            || (_clientHandshake == ClientHandshakeStage.AwaitingHelloAck && action is HelloAction);
    }

    public static void FlushInbox() => ProcessInboxOnMainThread();

    // --- lifecycle ---

    public static bool StartHost(int port = -1)
    {
        if (port < 0) port = ConfigManager.DefaultPort?.Value ?? MpConstants.DefaultPort;
        if (!Plugin.AllPatched)
        {
            Log.Fatal("Cannot start multiplayer: patch failure");
            return false;
        }
        if (_running) return true;

        StopInternal();
        _currentPort = port;
        _running = true;
        PlayerManager.Local.Id = ConfigManager.GetPlayerId();
        PlayerManager.Local.Uid = MpConstants.HostUid;
        TimeOffsetMs = 0;          // 主机是时间权威，自身偏移恒为 0
        LatencyMs = 0;
        Session.EnterDirectHostRoom();
        StartIoThread(() => _tcp.StartHost(port, ConfigManager.EnableIPv6?.Value ?? false));
        Log.LogInfo($"[MpWire] Host on port {port}");
        return true;
    }

    /// <summary>客机模式：仅设会话，连接由 <see cref="ConnectAsync"/> 完成。</summary>
    public static bool StartClientMode()
    {
        if (!Plugin.AllPatched) return false;
        if (_running) return true;
        _running = true;
        PlayerManager.Local.Id = ConfigManager.GetPlayerId();
        PlayerManager.Local.Uid = MpConstants.UnassignedUid;
        Session.EnterDirectClientRoom();
        StartIoThread(null);
        Log.LogInfo("[MpWire] Client mode (not connected)");
        return true;
    }

    public static void Stop()
    {
        if (!_running) return;
        _running = false;
        StopIoThread();
        Session.Reset();
        CancelSync();
        Log.LogInfo("[MpWire] Stopped");
    }

    public static bool RestartHost(int port) { Stop(); return StartHost(port); }

    public static async Task<bool> ConnectAsync(string host, int port = -1, bool switchFromHost = true)
    {
        if (port < 0) port = ConfigManager.DefaultPort?.Value ?? MpConstants.DefaultPort;
        if (!_running && !StartClientMode()) return false;
        if (HasTransportConnection)
        {
            Log.LogWarning("[C] Already connected");
            return false;
        }
        if (IsConnecting) return false;

        try
        {
            _connecting = true;
            if (switchFromHost && Session.IsRoomHost)
            {
                StopIoThread();
                Session.EnterDirectClientRoom();
                PlayerManager.Local.Uid = MpConstants.UnassignedUid;
                StartIoThread(null);
            }
            Session.EnterDirectClientRoom();
            _clientHandshake = ClientHandshakeStage.AwaitingInfo;
            RemoteConnectionInfo = null;
            var connection = _tcp;
            await Task.Run(() => connection.ConnectClient(host, port, ConnectTimeoutMs));
            PluginManager.RunOnMainThread(() =>
            {
                if (_tcp != connection || !_running || _clientHandshake != ClientHandshakeStage.AwaitingInfo) return;
                ConnectionInfoAction.Send();
                PluginHost.Instance.StartManagedCoroutine(WaitForHandshake(connection));
            });
            Log.LogMessage($"[C] TCP connected to {host}:{port}; awaiting version verification");
            return true;
        }
        catch (Exception e)
        {
            _clientHandshake = ClientHandshakeStage.None;
            Log.LogError($"[C] Connect failed: {e.Message}");
            return false;
        }
        finally
        {
            _connecting = false;
        }
    }

    public static void DisconnectPeer()
    {
        if (!Session.IsOnline) return;
        if (Session.TransportKind == TransportKind.DirectHost)
        {
            _tcp?.DisconnectAll();
            PlayerManager.ClearPeers();
            CancelSync();
        }
        else if (Session.TransportKind == TransportKind.DirectClient)
        {
            Stop();
        }
        else
        {
            PlayerManager.ClearPeers();
            Session.Reset();
            _running = false;
        }
        Log.LogMessage("[MpWire] Disconnected");
    }

    public static void DisconnectClient(int uid)
    {
        if (!Session.IsRoomHost) return;
        _peerHandshakes.TryRemove(uid, out _);
        _tcp?.DisconnectClient(uid);
        if (PlayerManager.Peers.ContainsKey(uid))
            OnHostClientLeft(uid);
    }

    // --- app send ---

    public static void EnqueueSend(Action action, bool lowPriority = false)
    {
        if (!CanSendAction(action)) return;

        var framed = NetPacket.FromAction(action).ToBytesWithLength();
        int? target = action.WireTargetUid;
        int? except = action.WireExceptUid;

        if (Session.IsRoomHost)
        {
            _outbox.Enqueue(new Outbound(framed, target, except, lowPriority,
                CompletesHandshake: action is HelloAckAction, CloseAfterSend: action is RejectAction));
            if (action is RejectAction && target is int uid)
                _peerHandshakes.TryRemove(uid, out _);
        }
        else if (Session.IsRoomClient)
            _outbox.Enqueue(new Outbound(framed, null, null, lowPriority));
    }

    public static long? UpdateLatency(int id)
    {
        if (!_pingSent.TryRemove(id, out long sentMs)) return null;
        LatencyMs = (NowMs - sentMs) / 2;
        return sentMs;
    }

    /// <summary>客机端：基于主机在收到 Ping 那一刻记录的时钟，估算本地与主机的时钟偏移。</summary>
    /// <param name="hostReceivedMs">主机收到 Ping 时的 NowMs（由 PongAction 携带回客机）。</param>
    /// <param name="sentMs">客机发出 Ping 时的本地 NowMs（由 UpdateLatency 返回）。</param>
    public static void UpdateTimeOffset(long hostReceivedMs, long sentMs)
    {
        // 主机收到 Ping 那一刻的主机时间 ≈ hostReceivedMs
        // 对应的客机本地时间 ≈ sentMs + LatencyMs（半个 RTT）
        // 时钟偏移 = 本地时间 - 主机时间
        long localEstimateAtHostReceive = sentMs + LatencyMs;
        TimeOffsetMs = localEstimateAtHostReceive - hostReceivedMs;
    }

    // --- session callbacks (from Actions / handshake) ---

    public static void OnConnectionInfoReceived(ConnectionInfoAction info)
    {
        if (Session.IsRoomHost)
        {
            if (!_peerHandshakes.TryAdd(info.SenderUid, new PeerHandshake(info))) return;
            ConnectionInfoAction.Send(info.SenderUid);
            return;
        }

        if (!Session.IsRoomClient || _clientHandshake != ClientHandshakeStage.AwaitingInfo) return;
        RemoteConnectionInfo = info;
        if (!info.MatchesVersions(Plugin.GameVersion, Plugin.ModVersion, Plugin.ProtocolVersion))
        {
            InGameConsole.LogError(TextId.ConnectionVersionMismatch.Get(
                Plugin.GameVersion, Plugin.ModVersion, Plugin.ProtocolVersion,
                info.GameVersion, info.ModVersion, info.ProtocolVersion));
            DisconnectPeer();
            return;
        }

        if (info.PlayerCount >= info.MaxPlayers)
        {
            InGameConsole.LogError(TextId.RoomFull.Get(info.PlayerCount, info.MaxPlayers));
            DisconnectPeer();
            return;
        }

        _clientHandshake = ClientHandshakeStage.AwaitingHelloAck;
        HelloAction.Send();
    }

    public static bool CanAcceptHello(int uid) =>
        _peerHandshakes.TryGetValue(uid, out var peer) && !peer.Complete
        && peer.Info.MatchesVersions(Plugin.GameVersion, Plugin.ModVersion, Plugin.ProtocolVersion);

    private static IEnumerator WaitForHandshake(DirectTcp connection)
    {
        long deadline = NowMs + ConnectTimeoutMs;
        while (_tcp == connection && _clientHandshake is ClientHandshakeStage.AwaitingInfo or ClientHandshakeStage.AwaitingHelloAck)
        {
            if (NowMs >= deadline)
            {
                InGameConsole.LogError(TextId.ConnectionHandshakeTimeout.Get());
                DisconnectPeer();
                yield break;
            }
            yield return null;
        }
    }

    public static void OnHandshakeComplete(string hostId)
    {
        _clientHandshake = ClientHandshakeStage.Complete;
        // 客机端：握手完成后，让 DirectTcp 把上行链路的 fromUid 同步为真实 HostUid
        _tcp?.SetUplinkUid(Session.HostUid);
        SceneTransitAction.Send(MpManager.LocalScene);
        CommandScheduler.EnqueueInterval(SyncActionCommandId, 0.5f, MoveSyncAction.Send);
        InGameConsole.ShowPassiveFromAnyThread(TextId.MultiplayerConnected.Get());
    }

    public static void OnPeerHandshakeComplete(int uid)
    {
        if (_peerHandshakes.TryGetValue(uid, out var peer))
            _peerHandshakes[uid] = peer with { Complete = true };
        CommandScheduler.EnqueueInterval(SyncActionCommandId, 2f, MoveSyncAction.Send);
    }

    // --- IO thread ---

    private static void StartIoThread(System.Action setup)
    {
        StopIoThread();
        _tcp = new DirectTcp(OnWirePacket, OnWirePeerLeft);
        setup?.Invoke();
        _ioRunning = true;
        _ioThread = new Thread(IoLoop) { IsBackground = true, Name = "MpWire-IO" };
        _ioThread.Start();
    }

    private static void StopIoThread()
    {
        _ioRunning = false;
        try { _ioThread?.Join(2000); } catch { }
        _ioThread = null;
        _tcp?.Stop();
        _tcp = null;
        _clientHandshake = ClientHandshakeStage.None;
        RemoteConnectionInfo = null;
        _peerHandshakes.Clear();
        _pingSent.Clear();
        _lastPingMs = 0;
        LatencyMs = 0;
        TimeOffsetMs = 0;
        while (_outbox.TryDequeue(out _)) { }
        while (_inbox.TryDequeue(out _)) { }
    }

    private static void StopInternal()
    {
        _running = false;
        StopIoThread();
    }

    private static void IoLoop()
    {
        while (_ioRunning)
        {
            try
            {
                while (_outbox.TryDequeue(out var msg))
                    _tcp?.Enqueue(msg.TargetUid, msg.ExceptUid, msg.Framed, msg.LowPriority, msg.CompletesHandshake, msg.CloseAfterSend);

                _tcp?.Pump();

                // 仅客机主动 Ping：主机是时间权威，无需估算时钟偏移或延迟。
                if (_running && Session.IsRoomClient && CanSend)
                {
                    long now = NowMs;
                    if (now - _lastPingMs >= PingIntervalMs)
                    {
                        _lastPingMs = now;
                        SendPingIo();
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"[MpWire] IO loop: {e.Message}");
            }
            Thread.Sleep(1);
        }
    }

    private static void SendPingIo()
    {
        int id = Interlocked.Increment(ref _pingId);
        _pingSent[id] = NowMs;
        var framed = NetPacket.FromAction(new PingAction { Id = id }).ToBytesWithLength();
        _tcp?.Enqueue(null, null, framed, false);
    }

    // 反序列化已在 PacketBuffer（IO 线程）。主机转发与出站共用 ToBytesWithLength，避免维护第二套组帧逻辑。
    private static void OnWirePacket(int fromUid, NetPacket packet)
    {
        var action = packet.Action;
        if (action == null) return;

        if (Session.IsRoomHost && action is not ConnectionInfoAction and not HelloAction
            && (!_peerHandshakes.TryGetValue(fromUid, out var peer) || !peer.Complete)) return;
        if (Session.IsRoomHost && fromUid != Session.HostUid && ShouldRelay(action))
        {
            action.SenderUid = fromUid;
            _outbox.Enqueue(new Outbound(
                NetPacket.FromAction(action).ToBytesWithLength(), null, fromUid, false));
        }

        _inbox.Enqueue(new Inbound(fromUid, action));
    }

    private static void OnWirePeerLeft(int uid)
    {
        _inbox.Enqueue(new Inbound(uid, null));
    }

    // 仅执行已反序列化 Action 的 OnReceived（Unity / PlayerManager）；转发已在 OnWirePacket（IO 线程）完成。
    private static void ProcessInboxOnMainThread()
    {
        while (_inbox.TryDequeue(out var item))
        {
            if (item.Action == null)
            {
                if (Session.IsRoomHost && item.FromUid != Session.HostUid)
                    OnHostClientLeft(item.FromUid);
                else if (Session.IsRoomClient)
                    OnClientDisconnected();
                continue;
            }

            // HelloAck 和后续业务包可能在同次读取中到达，须按处理顺序判断握手状态。
            if (Session.IsRoomClient && _clientHandshake != ClientHandshakeStage.Complete
                && item.Action is not ConnectionInfoAction and not HelloAckAction and not RejectAction) continue;

            try
            {
                // 主机：每条 TCP 连接对应真实 uid，可覆盖包体以防伪造。
                // 客机：线层 fromUid 为 Session.HostUid（握手后由 DirectTcp 上行链路 uid 提供），真实发送者已在主机转发时写入包体 SenderUid。
                if (Session.IsRoomHost)
                    item.Action.SenderUid = item.FromUid;
                item.Action.OnReceived();
            }
            catch (Exception e)
            {
                Log.LogError($"[MpWire] OnReceived failed for {item.Action.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    // 仅 DirectHost 且来自客机时由 OnWirePacket 调用；不必再判 RoomRole。
    private static bool ShouldRelay(Action action)
    {
        var t = action.GetType();
        return t.GetCustomAttribute<Action.RoomRelayAttribute>() != null
               || t.GetCustomAttribute<Action.PublicRelayAttribute>() != null;
    }

    private static void OnHostClientLeft(int uid)
    {
        _peerHandshakes.TryRemove(uid, out _);
        if (PlayerManager.Peers.TryGetValue(uid, out var peer))
        {
            var displayName = LiveModeManager.GetDisplayName(uid, peer.Id);
            InGameConsole.ShowPassiveFromAnyThread(TextId.PeerLeft.Get(displayName));
            PeerLeaveAction.Send(uid);
            PlayerManager.RemovePeer(uid);
            MpManager.CheckContinueAfterDisconnect(uid, displayName);
        }
        if (PlayerManager.Peers.IsEmpty) CancelSync();
    }

    private static void OnClientDisconnected()
    {
        bool wasConnected = _clientHandshake == ClientHandshakeStage.Complete;
        _clientHandshake = ClientHandshakeStage.None;
        RemoteConnectionInfo = null;
        while (_outbox.TryDequeue(out _)) { }
        PlayerManager.ClearPeers();
        PlayerManager.Local.Uid = MpConstants.UnassignedUid;
        CancelSync();
        InGameConsole.ShowPassiveFromAnyThread((wasConnected
            ? TextId.MultiplayerDisconnected : TextId.ConnectionHandshakeFailed).Get());
    }

    private static void CancelSync()
    {
        CommandScheduler.RemoveKeyFromKeyQueue(MpManager.PeerGetCharacterUnitNotNullCommand);
        CommandScheduler.CancelInterval(SyncActionCommandId);
    }
}

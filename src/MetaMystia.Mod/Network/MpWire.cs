using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MetaMystia.Network.Core;
using MetaMystia.Protocol;
using MetaMystia.UI;

namespace MetaMystia.Network;

// 仅负责装配与主线程投递。身份和成员由 ClientSession 应用 Endpoint 的确认。
[AutoLog]
public static partial class MpWire
{
    private sealed record ConnectResult(long Attempt, TcpConnection Connection, string Error);
    private static readonly ConcurrentQueue<ConnectResult> Connections = new();
    private static EndpointHost _host;
    private static TcpConnection _uplink;
    private static CancellationTokenSource _connectCancellation;
    private static TaskCompletionSource<bool> _connectCompletion;
    private static bool _enabled;
    private static long _nextMovement;
    public static ClientSession Session { get; } = new();
    public static bool IsRunning => _enabled;
    public static bool IsEmbeddedHost => _host != null;
    public static bool IsConnecting => Session.Stage is ConnectionStage.Connecting or ConnectionStage.Handshaking;
    public static bool IsRoomConnected => Session.CanPlay && Session.Room.Members.Count > 1;
    public static int CurrentPort { get; private set; } = MpConstants.DefaultPort;
    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static long SyncedNowMs => NowMs + Session.TimeOffsetMs;

    public static bool StartHost(int port = -1)
    {
        if (!Plugin.AllPatched) return false;
        if (!GameContext.CanEnterRoom) { InGameConsole.ShowPassive(TextId.MpRoomEntryUnavailable.Get()); return false; }
        int resolvedPort = port == -1 ? MpManager.ConfigPort : port;
        if (resolvedPort is < 1 or > 65535) { InGameConsole.ShowPassive(TextId.MpPortRange.Get()); return false; }
        Stop();
        CurrentPort = resolvedPort;
        try { _host = new EndpointHost(CurrentPort, MpManager.EnableIPv6); }
        catch (Exception error) when (error is SocketException or IOException)
        { Log.Error($"Cannot start endpoint: {error.Message}"); return false; }
        _enabled = true;
        long attempt = Session.BeginConnect(MpManager.PlayerId, ApplicationVersion, NowMs);
        _host.AttachLocal(NowMs);
        Session.TransportConnected(attempt, NowMs);
        FlushInbox();
        return true;
    }

    public static bool StartClientMode() { _enabled = true; return true; }
    private static string ApplicationVersion => $"MetaMystia/{Plugin.GameVersion}/{Plugin.ModVersion}/payload-{GameMessages.Version}";

    // 成功只表示握手完成；自动入直连房间的结果另由房间确认入口报告。
    public static Task<bool> ConnectAsync(string address, int port = -1, bool stopExistingServer = true)
    {
        if (!Plugin.AllPatched || IsConnecting || Session.IsOnline && (!stopExistingServer || _host == null)) return Task.FromResult(false);
        int resolvedPort = port == -1 ? MpManager.ConfigPort : port;
        if (resolvedPort is < 1 or > 65535) { InGameConsole.ShowPassive(TextId.MpPortRange.Get()); return Task.FromResult(false); }
        if (string.IsNullOrWhiteSpace(address)) return Task.FromResult(false);
        Stop();
        _enabled = true;
        CurrentPort = resolvedPort;
        long attempt = Session.BeginConnect(MpManager.PlayerId, ApplicationVersion, NowMs);
        _connectCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _connectCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = OpenConnection(attempt, address, CurrentPort, _connectCancellation.Token);
        return _connectCompletion.Task;
    }

    private static async Task OpenConnection(long attempt, string address, int port, CancellationToken cancellation)
    {
        try
        {
            var connection = await TcpConnection.ConnectAsync(address, port, cancellation).ConfigureAwait(false);
            Connections.Enqueue(new(attempt, connection, null));
        }
        catch (Exception error) when (error is SocketException or IOException or OperationCanceledException)
        { Connections.Enqueue(new(attempt, null, error.Message)); }
    }

    public static void Stop()
    {
        _enabled = false;
        _connectCancellation?.Cancel();
        _connectCancellation?.Dispose();
        _connectCancellation = null;
        _uplink?.Dispose();
        _uplink = null;
        _host?.Dispose();
        _host = null;
        _connectCompletion?.TrySetResult(false);
        _connectCompletion = null;
        Session.Disconnect(Session.Attempt, "Multiplayer stopped");
        ApplyChanges();
    }

    public static bool Request(RoomOperation operation, Guid roomId = default, string name = "", int targetUid = -1, bool admissionOpen = false)
    {
        if (operation is RoomOperation.Create or RoomOperation.Join && !GameContext.CanEnterRoom) return false;
        return Session.Request(operation, NowMs, roomId, name, Math.Clamp(ConfigManager.MaxPlayers.Value, 2, 64), targetUid, admissionOpen);
    }

    public static void DisconnectPeer() => Stop();
    public static bool DisconnectClient(int uid) => Request(RoomOperation.Kick, targetUid: uid);

    public static void FlushInbox()
    {
        while (Connections.TryDequeue(out var result))
        {
            if (result.Attempt != Session.Attempt || Session.Stage != ConnectionStage.Connecting)
            { result.Connection?.Dispose(); continue; }
            if (result.Connection == null) Session.Disconnect(result.Attempt, result.Error);
            else if (Session.TransportConnected(result.Attempt, NowMs)) _uplink = result.Connection;
            else result.Connection.Dispose();
        }
        Session.Tick(NowMs);
        ApplyChanges();
        for (int pass = 0; pass < 4; pass++)
        {
            while (Session.TryDequeue(out var outgoing))
            {
                if (_host != null) _host.ReceiveLocal(outgoing, NowMs);
                else _uplink?.Send(outgoing);
            }
            _host?.Pump(NowMs);
            while (_host != null && _host.TryDequeueLocal(out var delivery))
            {
                Session.Receive(Session.Attempt, delivery.Message, NowMs);
                ApplyChanges();
                if (delivery.Close) Session.Disconnect(Session.Attempt);
            }
            while (_uplink != null && _uplink.TryDequeue(out var received))
            {
                if (received.Message != null) Session.Receive(Session.Attempt, received.Message, NowMs);
                else Session.Disconnect(Session.Attempt, received.Error);
                ApplyChanges();
            }
        }
        RoomGameplay.Tick();
        if (Session.IsOnline && NowMs >= _nextMovement)
        {
            _nextMovement = NowMs + 50;
            MoveSyncAction.Send();
        }
        PlayerPresentation.Reconcile();
    }

    private static void ApplyChanges()
    {
        while (Session.TryDequeueEvent(out var change))
        {
            switch (change.Kind)
            {
                case SessionEventKind.Online:
                    PlayerChangeSkinAction.Send(PlayerManager.Local.Skin);
                    SceneTransitAction.Send(MpManager.LocalScene);
                    _connectCompletion?.TrySetResult(true);
                    _connectCompletion = null;
                    if (_host != null) Request(RoomOperation.Create, name: MpManager.PlayerId);
                    else if (Session.DefaultRoom != Guid.Empty) Request(RoomOperation.Join, Session.DefaultRoom);
                    InGameConsole.ShowPassive(TextId.MpPublicOnline.Get());
                    break;
                case SessionEventKind.StateChanged:
                    ModPlayerStore.ReconcileSession();
                    RoomGameplay.OnSessionChanged();
                    break;
                case SessionEventKind.Payload:
                    GameMessages.Receive(change.Payload);
                    break;
                case SessionEventKind.RequestCompleted:
                    Log.Message($"Room request {change.Operation}: {(change.Reconciled ? "reconciled" : change.Detail.Length == 0 ? "confirmed" : change.Detail)}; binding={Session.Binding}");
                    if (change.Reconciled)
                    {
                        InGameConsole.ShowPassive(TextId.MpRequestReconciled.Get());
                        RoomGameplay.OnRequestCompleted(change.Operation, change.Detail);
                        break;
                    }
                    if (change.Detail.Length > 0) InGameConsole.ShowPassive(NetworkText.Error(change.Detail));
                    else if (change.Operation == RoomOperation.Create && Session.Room != null)
                        InGameConsole.ShowPassive(TextId.MpRoomCreated.Get(Session.Room.Name));
                    else if (change.Operation == RoomOperation.Join && Session.Room != null)
                        InGameConsole.ShowPassive(TextId.MpRoomJoined.Get(Session.Room.Name));
                    else if (change.Operation == RoomOperation.SetCapacity && Session.Room != null)
                        InGameConsole.ShowPassive(TextId.MpMaxPlayersSet.Get(Session.Room.Capacity));
                    else if (change.Operation == RoomOperation.Rename)
                        InGameConsole.ShowPassive(TextId.MpPlayerIdSet.Get(PlayerManager.Local.Id));
                    if (change.Operation == RoomOperation.Create && _host != null && Session.Room != null) _host.Endpoint.DefaultRoom = Session.Room.Id;
                    RoomGameplay.OnRequestCompleted(change.Operation, change.Detail);
                    break;
                case SessionEventKind.Disconnected:
                    _connectCancellation?.Cancel();
                    _uplink?.Dispose();
                    _uplink = null;
                    _connectCompletion?.TrySetResult(false);
                    _connectCompletion = null;
                    ModPlayerStore.ReconcileSession();
                    RoomGameplay.OnSessionChanged();
                    PlayerPresentation.Reconcile();
                    if (_enabled)
                    {
                        Log.Warning($"Connection ended: {change.Detail}; attempt={Session.Attempt}");
                        InGameConsole.ShowPassive(NetworkText.Error(change.Detail, TextId.MpConnectionClosed));
                    }
                    break;
            }
        }
    }
}

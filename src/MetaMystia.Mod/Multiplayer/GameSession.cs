using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MetaMystia.Multiplayer.Actions;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia.Multiplayer;

/// <summary>游戏端连接入口；服务器和客户端拥有全部网络状态。</summary>
[AutoLog]
public static partial class GameSession
{
    public const int DefaultPort = 40815;
    public static int ConfigPort => ConfigManager.DefaultPort?.Value ?? DefaultPort;
    public static int CurrentPort { get; private set; } = DefaultPort;
    public static bool EnableIPv6 => ConfigManager.EnableIPv6?.Value ?? false;
    public static Client Client { get; private set; }
    public static Snapshot State { get; private set; } = new();
    public static Room Room => State.Room;
    public static bool IsOnline => Client?.IsConnected == true;
    public static bool IsConnecting { get; private set; }
    public static bool IsRunning => IsConnecting || IsOnline;
    public static bool IsRoomHost => IsOnline && Room?.Host == Client.Uid;
    public static bool IsRoomClient => IsOnline && Room != null && !IsRoomHost;
    public static bool IsInRoom => IsOnline && Room != null;
    public static bool HasPeers => IsInRoom && Room.Members.Length > 1;
    public static bool IsLanHost => lan != null;
    public static long Membership => Room?.Members.FirstOrDefault(p => p.Uid == Client?.Uid)?.Membership ?? 0;
    private static LanSession lan;
    private static CancellationTokenSource cancellation;
    private static Task shutdown = Task.CompletedTask;
    private static Task joinability = Task.CompletedTask;
    private static long nextMotion;
    private static GameStage publishedStage;

    public static void StartHost(int port = -1)
    {
        if (IsLanHost && IsRunning) { InGameConsole.ShowPassive(TextId.MpAlreadyStarted.Get("Host")); return; }
        if (!CanStart()) return;
        Stop();
        CurrentPort = port < 0 ? ConfigPort : port;
        lan = new(CurrentPort, ConfigManager.MaxPlayers.Value, GameMessageRules.Create(),
            new(Versions.Current.Protocol, Plugin.GameVersion, Plugin.ModVersion), EnableIPv6);
        var session = lan;
        Attach(session.Client);
        var player = PlayerProfile.Capture();
        var token = cancellation.Token;
        var previous = shutdown;
        Run(Start(), () => InGameConsole.ShowPassive(TextId.MpStartedOnPort.Get(CurrentPort)), connecting: true);
        async Task Start()
        {
            await previous.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await session.StartAsync(player, token).ConfigureAwait(false);
        }
    }

    public static void Connect(string host, int port = -1, bool world = false)
    {
        if (IsConnecting || HasPeers || !CanStart()) return;
        Stop();
        CurrentPort = port < 0 ? ConfigPort : port;
        var client = new Client(new(Versions.Current.Protocol, Plugin.GameVersion, Plugin.ModVersion));
        Attach(client);
        var player = PlayerProfile.Capture();
        var token = cancellation.Token;
        int targetPort = CurrentPort;
        bool ipv6 = EnableIPv6;
        Run(Connect(), () => InGameConsole.ShowPassive(TextId.MultiplayerConnected.Get()), connecting: true);
        async Task Connect()
        {
            var addresses = await Dns.GetHostAddressesAsync(host.Trim('[', ']')).WaitAsync(token).ConfigureAwait(false);
            var address = addresses.FirstOrDefault(a => ipv6 || a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?? throw new NetworkException(NetworkErrorCode.AddressUnavailable);
            var endpoint = new IPEndPoint(address, targetPort);
            if (world) await client.ConnectAsync(endpoint, player, token).ConfigureAwait(false);
            else await LanSession.JoinAsync(client, endpoint, player, token).ConfigureAwait(false);
        }
    }

    private static bool CanStart()
    {
        if (!Plugin.AllPatched || !GameFlow.IsMultiplayerAvailable || !GameFlow.CanJoin)
        {
            InGameConsole.LogError((Plugin.AllPatched ? TextId.MpMainSceneRequired : TextId.ModPatchFailure).Get());
            return false;
        }
        PlayerManager.Local.ReloadResourceTable();
        if (PlayerManager.Local.IncrementalDataBase.IsIncrementalReady) return true;
        InGameConsole.LogError(TextId.GameResourcesNotLoaded.Get());
        return false;
    }

    private static void Attach(Client client)
    {
        Client = client;
        cancellation = new();
        IsConnecting = true;
        publishedStage = GameFlow.Stage;
        joinability = Task.CompletedTask;
        client.CallbackError += error => Log.Error($"Network callback: {error}");
        if (lan != null) lan.Server.CallbackError += error => Log.Error($"Server callback: {error}");
        client.StateChanged += () => ApplyState(client);
        client.MessageReceived += GameActions.Receive;
        client.ConnectionEnded += reason =>
        {
            if (Client != client) return;
            Stop();
            Log.Info($"Connection ended: {reason}");
            InGameConsole.ShowPassive(NetworkNotice.Describe(reason));
        };
    }

    private static void ApplyState(Client client)
    {
        if (Client != client) return;
        var previous = Membership;
        State = client.State;
        PlayerManager.Local.Uid = client.Uid;
        PlayerManager.Local.Id = PlayerIdentity.Name;
        if (previous != Membership)
        {
            BusinessStart.Reset(resume: previous != 0 && Membership == 0);
            GameFlow.ResetGameplay();
            DayDestinationManager.ResetSession();
            YuyukoGuestSync.Reset();
            RoomClock.Reset();
        }
        PlayerManager.ApplyNetworkState(State, previous != Membership);
    }

    public static void Tick()
    {
        Client?.DispatchPending();
        if (!IsOnline) return;
        if (publishedStage != GameFlow.Stage)
        {
            publishedStage = GameFlow.Stage;
            PlayerProfile.SendProfile();
        }
        DayDestinationManager.TryConfirm();
        BusinessStart.TryStart();
        if (IsRoomHost && GameFlow.IsCooperative)
        {
            PrepSceneManager.TryCompletePrep();
            if (GameFlow.LocalScene == Common.UI.Scene.DayScene && !GameFlow.IsFinalTrial)
                Patch.IzakayaSelectorPanelPatch.TryConfirmSelection();
        }
        if (IsRoomHost && !IsConnecting && joinability.IsCompleted && Room.Joinable != GameFlow.CanOpenRoom)
            SetJoinable(GameFlow.CanOpenRoom);
        if (RoomClock.Now >= nextMotion)
        {
            nextMotion = RoomClock.Now + 2000;
            PlayerProfile.SendMotion();
        }
        RoomClock.Tick();
    }

    public static Task SetJoinable(bool allowed)
    {
        if (!IsRoomHost) return Task.CompletedTask;
        var client = Client;
        var token = cancellation.Token;
        var previous = joinability;
        joinability = Change();
        Run(joinability);
        return joinability;
        async Task Change()
        {
            await previous.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await client.SetJoinableAsync(allowed, token).ConfigureAwait(false);
        }
    }

    public static void CreateRoom(int maxPlayers) => Run(Client.CreateRoomAsync(maxPlayers, cancellation.Token));
    public static void JoinRoom(long room) => Run(Client.JoinRoomAsync(room, cancellation.Token));
    public static void Kick(int uid) => Run(Client.KickAsync(uid, cancellation.Token));
    public static void SetPlayerLimit(int count) => Run(Client.SetRoomPlayerLimitAsync(count, cancellation.Token),
        () => ConfigManager.MaxPlayers.Value = count);

    public static void LeaveRoom()
    {
        if (!IsInRoom) return;
        Client.LeaveRoom();
        ApplyState(Client);
    }

    public static void Stop(bool resumeGameplay = true)
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        Client?.Disconnect();
        Client = null;
        if (lan != null) shutdown = Task.WhenAll(shutdown, lan.DisposeAsync().AsTask());
        lan = null;
        IsConnecting = false;
        State = new();
        BusinessStart.Reset(resumeGameplay);
        GameFlow.ResetGameplay();
        PlayerManager.ClearPeers();
        PlayerManager.Local.Uid = 0;
        YuyukoGuestSync.Reset();
        RoomClock.Reset();
    }

    public static void Restart()
    {
        var port = CurrentPort;
        Stop();
        StartHost(port);
    }

    private static void Run(Task operation, System.Action completed = null, bool connecting = false) =>
        PluginHost.Instance.StartManagedCoroutine(Observe(Client, operation, completed, connecting));

    private static IEnumerator Observe(Client client, Task operation, System.Action completed, bool connecting)
    {
        while (!operation.IsCompleted) yield return null;
        var error = operation.Exception?.GetBaseException();
        if (Client != client) yield break;
        if (connecting) IsConnecting = false;
        if (!operation.IsCompletedSuccessfully)
        {
            Log.Error($"Network operation failed: {error?.Message ?? "Cancelled"}");
            InGameConsole.LogError(NetworkNotice.Describe(error));
            if (!client.IsConnected) Stop();
            yield break;
        }
        ApplyState(client);
        completed?.Invoke();
    }
}

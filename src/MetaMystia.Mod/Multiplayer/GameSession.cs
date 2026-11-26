using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia.Multiplayer;

/// <summary>游戏端连接入口；服务器和客户端拥有全部网络状态。</summary>
public static class GameSession
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
    public static bool IsConnectingOrOnline => IsConnecting || IsOnline;
    public static bool IsRoomHost => IsOnline && Room?.Host == Client.Uid;
    public static bool IsRoomClient => IsOnline && Room != null && !IsRoomHost;
    public static bool IsInRoom => IsOnline && Room != null;
    public static bool HasRoomPeers => IsInRoom && Room.Members.Length > 1;
    public static bool HasLocalServer => lan != null;
    public static long RoomMembershipId => Room?.Members.FirstOrDefault(p => p.Uid == Client?.Uid)?.Membership ?? 0;
    private static LanSession lan;
    private static CancellationTokenSource cancellation;
    private static Task shutdown = Task.CompletedTask;
    private static Task joinability = Task.CompletedTask;
    private static long nextMotion;
    private static GameStage publishedStage;

    public static void StartHost(int port = -1)
    {
        if (HasLocalServer && IsConnectingOrOnline) { InGameConsole.ShowPassive(TextId.MpAlreadyStarted.Get("Host")); return; }
        if (!CanStart()) return;
        Stop();
        CurrentPort = port < 0 ? ConfigPort : port;
        lan = new(CurrentPort, ConfigManager.MaxPlayers.Value,
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

    public static void ConnectOfficial() => Connect("play.metamystia.net", DefaultPort);

    public static void Connect(string host, int port = -1)
    {
        if (IsConnectingOrOnline || !CanStart()) return;
        Stop();
        CurrentPort = port < 0 ? ConfigPort : port;
        var client = new Client(new(Versions.Current.Protocol, Plugin.GameVersion, Plugin.ModVersion));
        Attach(client);
        var player = PlayerProfile.Capture();
        var token = cancellation.Token;
        int targetPort = CurrentPort;
        bool ipv6 = EnableIPv6;
        Run(Connect(), () =>
        {
            CurrentPort = targetPort;
            InGameConsole.ShowPassive(TextId.MultiplayerConnected.Get());
        }, connecting: true);
        async Task Connect()
        {
            var addresses = await Dns.GetHostAddressesAsync(host.Trim('[', ']')).WaitAsync(token).ConfigureAwait(false);
            var address = addresses.FirstOrDefault(a => ipv6 || a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?? throw new NetworkException(NetworkErrorCode.AddressUnavailable);
            var endpoint = new IPEndPoint(address, targetPort);
            await client.ConnectAsync(endpoint, player, token).ConfigureAwait(false);
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
        client.RosterChanged += () => ApplyState(client);
        client.MotionChanged += (uid, motion) =>
        {
            if (Client != client) return;
            State = State.WithMotion(uid, motion, out var player);
            if (player != null && PlayerManager.TryGetVisiblePeer(uid, out var peer))
                peer.ApplyMotion(player);
        };
        client.MessageReceived += GameMessages.Receive;
        client.ChatRejected += error => InGameConsole.LogError(NetworkNotice.Describe(error));
        client.NoticeReceived += notice =>
        {
            if (Client == client && notice.Code == ServerNoticeCode.Retiring)
                InGameConsole.AddChat(TextId.ServerChatName.Get(), TextId.NetworkUpdateNotice.Get(notice.TargetModVersion), fromServer: true);
        };
        client.ConnectionEnded += reason =>
        {
            if (Client != client) return;
            Log.Info($"Connection ended: {reason}");
            if (reason.Code is NetworkErrorCode.ServerFull or NetworkErrorCode.RoomFull)
                InGameConsole.LogError(NetworkNotice.Describe(reason));
            else InGameConsole.ShowPassive(NetworkNotice.Describe(reason));
            Stop();
        };
    }

    private static void ApplyState(Client client, bool resumeGameplay = true)
    {
        if (Client != client) return;
        var previous = RoomMembershipId;
        var previousState = State;
        State = client.State;
        PlayerPresenceNotice.ShowChanges(previousState, State, client.Uid);
        PlayerManager.Local.Uid = client.Uid;
        PlayerManager.Local.Id = PlayerIdentity.Name;
        if (previous != RoomMembershipId)
        {
            GameFlow.ResetGameplay(resume: resumeGameplay && previous != 0 && RoomMembershipId == 0);
            DayDestinationManager.ResetSession();
            YuyukoGuestSync.Reset();
        }
        PlayerManager.ApplyNetworkState(State, previous != RoomMembershipId);
    }

    internal static void SetLocalProfile(Skin skin)
    {
        Client.SetProfile(PlayerIdentity.Name, skin, GameFlow.LocalScene, GameFlow.Stage);
        State = Client.State;
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
        RoomTimeStop.Tick();
        if (IsRoomHost && GameFlow.IsCooperative)
        {
            PrepSceneManager.TryCompletePrep();
            if (GameFlow.LocalScene == Common.UI.Scene.DayScene && !GameFlow.IsFinalTrial)
                Patch.IzakayaSelectorPanelPatch.TryConfirmSelection();
        }
        if (IsRoomHost && !IsConnecting && joinability.IsCompleted && Room.Joinable != GameFlow.CanOpenRoom)
            SetJoinable(GameFlow.CanOpenRoom);
        if (Environment.TickCount64 >= nextMotion)
        {
            nextMotion = Environment.TickCount64 + 2000;
            PlayerProfile.SendMotion();
        }
    }

    public static Task SetJoinable(bool allowed, System.Action<bool> completed = null)
    {
        if (!IsRoomHost) return Task.CompletedTask;
        var client = Client;
        var token = cancellation.Token;
        var previous = joinability;
        joinability = Change();
        Run(joinability, () => completed?.Invoke(true), failed: () => completed?.Invoke(false));
        return joinability;
        async Task Change()
        {
            await previous.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await client.SetJoinableAsync(allowed, token).ConfigureAwait(false);
        }
    }

    public static void CreateRoom(int maxPlayers) => Run(Client.CreateRoomAsync(maxPlayers, cancellation.Token),
        () => InGameConsole.LogSuccess(TextId.NetworkRoomCreated.Get(RoomCode.Format(Room.Id))));
    public static void JoinRoom(ushort room) => Run(Client.JoinRoomAsync(room, cancellation.Token),
        () => InGameConsole.LogSuccess(TextId.NetworkRoomJoined.Get(RoomCode.Format(Room.Id))));
    public static void Kick(int uid) => Run(Client.KickAsync(uid, cancellation.Token));
    public static void SetPlayerLimit(int count) => Run(Client.SetRoomPlayerLimitAsync(count, cancellation.Token),
        () =>
        {
            ConfigManager.MaxPlayers.Value = count;
            InGameConsole.LogSuccess(TextId.MpMaxPlayersSet.Get(count));
        });

    public static void LeaveRoom(bool resumeGameplay = true)
    {
        if (!IsInRoom) return;
        if (State.IsLan) { Stop(resumeGameplay); return; }
        Client.LeaveRoom();
        ApplyState(Client, resumeGameplay);
    }

    public static void Stop(bool resumeGameplay = true)
    {
        var wasOnline = IsOnline;
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        Client?.Disconnect();
        Client = null;
        if (lan != null) shutdown = Task.WhenAll(shutdown, lan.DisposeAsync().AsTask());
        lan = null;
        IsConnecting = false;
        State = new();
        GameFlow.ResetGameplay(resume: resumeGameplay);
        PlayerManager.ClearPeers();
        PlayerManager.Local.Uid = 0;
        YuyukoGuestSync.Reset();
        if (wasOnline) InGameConsole.ShowPassive(TextId.MultiplayerDisconnected.Get());
    }

    public static void Restart()
    {
        var port = CurrentPort;
        Stop();
        StartHost(port);
    }

    private static void Run(Task operation, System.Action completed = null, bool connecting = false, System.Action failed = null) =>
        PluginHost.Instance.StartManagedCoroutine(Observe(Client, operation, completed, connecting, failed));

    private static IEnumerator Observe(Client client, Task operation, System.Action completed, bool connecting, System.Action failed)
    {
        while (!operation.IsCompleted) yield return null;
        var error = operation.Exception?.GetBaseException();
        if (Client != client) yield break;
        if (connecting) IsConnecting = false;
        if (!operation.IsCompletedSuccessfully)
        {
            Log.Error($"Network operation failed: {error?.Message ?? "Cancelled"}");
            InGameConsole.LogError(NetworkNotice.Describe(error));
            failed?.Invoke();
            if (!client.IsConnected) Stop();
            yield break;
        }
        ApplyState(client);
        completed?.Invoke();
    }
}

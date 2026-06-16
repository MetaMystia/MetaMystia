using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MetaMystia.Network;
using MetaMystia.Network.Services;
using MetaMystia.Patch;
using MetaMystia.Protocol.Transport;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

/// <summary>联机应用层：场景、阶段、剧情；线层见 <see cref="MpWire"/>。</summary>
[AutoLog]
public static partial class MpManager
{
    public enum ROLE { Server, Client }

    public const int DEFAULT_PORT = MpConstants.DefaultPort;
    public const int HOST_UID = MpConstants.HostUid;
    public const int UNASSIGNED_UID = MpConstants.UnassignedUid;
    public const string PeerGetCharacterUnitNotNullCommand = "PeerGetCharacterUnitNotNullCommand";

    public static int ConfigPort => ConfigManager.DefaultPort?.Value ?? DEFAULT_PORT;
    public static int CurrentPort => MpWire.CurrentPort;
    public static bool EnableIPv6 => ConfigManager.EnableIPv6?.Value ?? false;

    public static MpSession Session => MpWire.Session;
    public static bool IsRunning => MpWire.IsRunning;
    public static bool IsConnecting => MpWire.IsConnecting;
    public static bool IsOnline => Session.IsOnline;
    public static bool IsInRoom => Session.IsInRoom;
    public static bool IsInPublicScope => Session.IsInPublicScope;
    public static bool IsRoomHost => Session.IsRoomHost;
    public static bool IsRoomClient => Session.IsRoomClient;
    public static bool IsDirectHost => Session.TransportKind == TransportKind.DirectHost;
    public static bool IsDirectClient => Session.TransportKind == TransportKind.DirectClient;
    public static bool IsRelayClient => Session.IsRelay;
    public static bool HasRoomConnection => MpWire.IsRoomConnected;
    public static bool IsConnected => Session.IsInRoom && MpWire.IsRoomConnected;
    public static bool IsConnectedClient => IsRoomClient && IsConnected;
    public static bool IsConnectedServer => IsRoomHost && IsConnected;
    public static bool IsServer => IsRoomHost;
    public static bool IsClient => IsRoomClient;
    public static bool CanSeeOnlinePlayers => IsRunning && (Session.IsInRoom || Session.IsInPublicScope);

    public static bool LocalIsDayOver => PlayerManager.LocalIsDayOver;
    public static bool LocalIsPrepOver => PlayerManager.LocalIsPrepOver;

    public static string PlayerId { get => ConfigManager.GetPlayerId(); set => ConfigManager.SetPlayerId(value); }
    public static long Latency => MpWire.LatencyMs;
    public static long TimestampNow => MpWire.NowMs;
    public static long TimeOffset { get => MpWire.TimeOffsetMs; set => MpWire.TimeOffsetMs = value; }
    public static long GetSynchronizedTimestampNow => MpWire.SyncedNowMs;

    public static int ConnectedPlayersCount => PlayerManager.Peers.Count;
    public static int AllPlayersCount => ConnectedPlayersCount + 1;

    public static string RoleTag => IsRoomHost ? "[H]" : IsRoomClient ? "[C]" : "[N]";
    public static string RoleName => IsRoomHost ? "Host" : IsRoomClient ? "Client" : "Offline";

    public static Common.UI.Scene LocalScene { get; private set; } = Common.UI.Scene.EmptyScene;
    public static Common.UI.Scene PeerScene = Common.UI.Scene.EmptyScene;

    /// <summary>至少进入过一次主界面后，才允许开服或连接主机。</summary>
    public static bool IsMultiplayerAvailable { get; private set; }

    public static int WorkTimeSecondOverride = 9 * 60;

    private static bool _inStory;
    public static bool InStory => _inStory;
    public static bool IsGameplaySyncActive => IsInRoom && HasRoomConnection && !InStory;
    public static bool ShouldSkipAction => !IsGameplaySyncActive;

    public static void RefreshInStoryCache()
    {
        var director = Common.SceneDirector.Instance?.playableDirector;
        _inStory = director != null &&
            (director.state == UnityEngine.Playables.PlayState.Playing
             || director.state == UnityEngine.Playables.PlayState.Delayed);
    }

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

    public static bool Start(ROLE r = ROLE.Server, int port = -1)
    {
        if (!EnsureMultiplayerAvailable()) return false;
        return r == ROLE.Server ? MpWire.StartHost(port) : MpWire.StartClientMode();
    }

    public static void Stop() => MpWire.Stop();

    public static bool Restart()
    {
        var port = CurrentPort;
        Stop();
        return Start(ROLE.Server, port);
    }

    public static Task<bool> ConnectToPeerAsync(string peerIp, int port = -1, bool stop_existed_server = true)
    {
        if (!EnsureMultiplayerAvailable()) return Task.FromResult(false);
        return MpWire.ConnectAsync(peerIp, port, stop_existed_server);
    }

    public static void DisconnectPeer() => MpWire.DisconnectPeer();

    public static void DisconnectClient(int uid) => MpWire.DisconnectClient(uid);

    public static bool EnterRelayPublic()
    {
        if (!EnsureMultiplayerAvailable()) return false;
        MpWire.StartClientMode();
        Session.EnterRelayPublic();
        return true;
    }

    public static bool EnterRelayRoomAsHost(string roomId, int hostUid = HOST_UID)
    {
        if (!EnsureMultiplayerAvailable()) return false;
        MpWire.StartHost();
        PlayerManager.Local.Uid = hostUid;
        Session.EnterRelayRoom(RoomRole.Host, roomId, hostUid);
        return true;
    }

    public static bool EnterRelayRoomAsClient(string roomId, int localUid, int hostUid = HOST_UID)
    {
        if (!EnsureMultiplayerAvailable()) return false;
        MpWire.StartClientMode();
        PlayerManager.Local.Uid = localUid;
        Session.EnterRelayRoom(RoomRole.Client, roomId, hostUid);
        return true;
    }

    public static void CheckContinueAfterDisconnect(int disconnectedUid, string disconnectedName)
    {
        if (!IsRoomHost) return;
        disconnectedName ??= $"uid={disconnectedUid}";
        bool hasPeers = !PlayerManager.Peers.IsEmpty;
        switch (LocalScene)
        {
            case Common.UI.Scene.DayScene when LocalIsDayOver:
                InGameConsole.ShowPassiveFromAnyThread(
                    hasPeers && !PlayerManager.AllPeersDayOver
                        ? TextId.PeerDisconnectedWaiting.Get(disconnectedName)
                        : TextId.PeerDisconnectedAllReady.Get(disconnectedName, "/mp continue day"));
                break;
            case Common.UI.Scene.IzakayaPrepScene when LocalIsPrepOver:
                InGameConsole.ShowPassiveFromAnyThread(
                    hasPeers && !PlayerManager.AllPeersPrepOver
                        ? TextId.PeerDisconnectedWaiting.Get(disconnectedName)
                        : TextId.PeerDisconnectedAllReady.Get(disconnectedName, "/mp continue prep"));
                break;
        }
    }

    public static string GetStatus()
    {
        var status = new StringBuilder();
        status.AppendLine($"Self: {RoleTag} {PlayerId} (uid={PlayerManager.Local.Uid})");
        status.AppendLine($"Port: {CurrentPort} | Running: {(IsRunning ? "Yes" : "No")} | Connected: {(IsConnected ? "Yes" : "No")}");
        if (IsConnected)
        {
            status.AppendLine($"Ping: {Latency} ms | Players: {AllPlayersCount}");
            foreach (var kvp in PlayerManager.Peers)
                status.AppendLine($"  Peer: {(kvp.Key == HOST_UID ? "[S]" : "[C]")} {kvp.Value.Id} (uid={kvp.Key})");
        }
        return status.ToString();
    }

    public static string BriefStatus
    {
        get
        {
            if (!Plugin.AllPatched)
                return $"{TextId.ModPatchFailure.Get()} {BriefDebugText}";
            if (!IsRunning) return "Multiplayer: Off";
            if (IsConnected)
            {
                if (LiveModeManager.Mode == LiveMode.Partial)
                    return $"MP: {RoleTag} | {AllPlayersCount}Players | ping {Latency}ms";

                var peerNames = string.Join(", ",
                    PlayerManager.Peers.Values.Select(p => LiveModeManager.GetDisplayName(p.Uid)));
                return $"MP: {RoleTag} uid={PlayerManager.Local.Uid} | {AllPlayersCount}Players | ping {Latency}ms | {peerNames}";
            }
            return $"MP: {RoleName} (not connected)";
        }
    }

    public static string DebugText => $"{BriefDebugText}\n{BriefStatus}";

    private static string BriefDebugText =>
        $"{Plugin.GameVersion}: {Plugin.ModVersion}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}, {DateTimeOffset.Now}";

    public static void OnSceneTransit(Common.UI.Scene newScene)
    {
        Log.Message($"LocalScene transit from {LocalScene} -> {newScene}");
        CommonServices.SendSceneTransit(newScene);
        LocalScene = newScene;
        if (newScene != Common.UI.Scene.MainScene) return;

        IsMultiplayerAvailable = true;

        if (IsConnected)
        {
            Log.Message($"Transit to {newScene}, disconnecting peers");
            DisconnectPeer();
        }
        else if (!PlayerManager.Peers.IsEmpty)
        {
            PlayerManager.ClearPeers();
            CommandScheduler.RemoveKeyFromKeyQueue(PeerGetCharacterUnitNotNullCommand);
            CommandScheduler.CancelInterval(MpWire.SyncMessageCommandId);
        }
    }

    private static bool EnsureMultiplayerAvailable()
    {
        if (!IsMultiplayerAvailable)
        {
            NotifyMpBlocked(TextId.MpMainSceneRequired);
            return false;
        }

        PlayerManager.Local.ReloadResourceTable();
        if (!PlayerManager.Local.IncrementalDataBase.IsIncrementalReady)
        {
            NotifyMpBlocked(TextId.GameResourcesNotLoaded);
            return false;
        }

        return true;
    }

    private static void NotifyMpBlocked(TextId reason)
    {
        InGameConsole.ShowPassiveFromAnyThread(reason.Get());
        Log.LogWarning($"Multiplayer blocked: {reason}");
    }

    public static void DayOver()
    {
        if (!IsConnectedServer) return;
        if (PlayerManager.AllDayOver)
        {
            CommonServices.SendDayAllReady();
            CommandScheduler.EnqueueWithNoCondition(() =>
            {
                InGameConsole.ShowPassive(TextId.AllReadyTransition.Get());
                DaySceneManagerPatch.OnDayOver();
            });
        }
    }

    public static void PrepOver()
    {
        if (!IsConnectedServer) return;
        if (PlayerManager.AllPrepOver)
        {
            PrepSceneServices.SendPrepAllReady();
            CommandScheduler.EnqueueWithNoCondition(IzakayaConfigPannelPatch.PrepOver);
        }
    }

    public static bool ContinueDay()
    {
        if (!IsRoomHost || LocalScene != Common.UI.Scene.DayScene || !LocalIsDayOver) return false;
        foreach (var peer in PlayerManager.Peers.Values) peer.IsDayOver = true;
        CommonServices.SendDayAllReady();
        CommandScheduler.EnqueueWithNoCondition(() =>
        {
            InGameConsole.ShowPassive(TextId.AllReadyTransition.Get());
            DaySceneManagerPatch.OnDayOver();
        });
        return true;
    }

    public static bool ContinuePrep()
    {
        if (!IsRoomHost || (LocalScene != Common.UI.Scene.IzakayaPrepScene && LocalScene != Common.UI.Scene.WorkScene) || !LocalIsPrepOver)
            return false;
        foreach (var peer in PlayerManager.Peers.Values) peer.IsPrepOver = true;
        PrepSceneServices.SendPrepAllReady();
        CommandScheduler.EnqueueWithNoCondition(IzakayaConfigPannelPatch.PrepOver);
        return true;
    }
}

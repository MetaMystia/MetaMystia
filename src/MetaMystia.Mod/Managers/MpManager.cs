using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MetaMystia.Network;
using MetaMystia.Network.Core;
using MetaMystia.Protocol;
using MetaMystia.UI;

namespace MetaMystia;

// 应用入口与只读便捷访问；连接、成员、玩法、表现分别由对应模块维护。
public static class MpManager
{
    public enum ROLE { Server, Client }
    public const int DEFAULT_PORT = MpConstants.DefaultPort;
    public const int UNASSIGNED_UID = -1;
    public static int ConfigPort => ConfigManager.DefaultPort?.Value ?? DEFAULT_PORT;
    public static int CurrentPort => MpWire.CurrentPort;
    public static bool EnableIPv6 => ConfigManager.EnableIPv6?.Value ?? false;
    public static ClientSession Session => MpWire.Session;
    public static bool IsRunning => MpWire.IsRunning;
    public static bool IsConnecting => MpWire.IsConnecting;
    public static bool IsOnline => Session.IsOnline;
    public static bool IsInRoom => Session.IsInRoom;
    public static bool IsInPublicScope => Session.IsOnline;
    public static bool IsRoomHost => Session.IsRoomHost;
    public static bool IsRoomClient => Session.IsRoomClient;
    public static bool IsDirectHost => MpWire.IsEmbeddedHost;
    public static bool IsDirectClient => !IsDirectHost && Session.DefaultRoom != Guid.Empty;
    public static bool IsRelayClient => IsOnline && !IsDirectHost && !IsDirectClient;
    public static bool HasRoomConnection => MpWire.IsRoomConnected;
    public static bool IsConnected => HasRoomConnection && RoomGameplay.IsReady;
    public static bool IsConnectedClient => IsRoomClient && IsConnected;
    public static bool IsConnectedServer => IsRoomHost && IsConnected;
    public static bool IsServer => IsRoomHost;
    public static bool IsClient => IsRoomClient;
    public static bool CanSeeOnlinePlayers => IsOnline;
    public static bool LocalIsDayOver => RoomGameplay.LocalDayReady;
    public static bool LocalIsPrepOver => RoomGameplay.LocalPrepReady;
    public static string PlayerId { get => ConfigManager.GetPlayerId(); set => ConfigManager.SetPlayerId(value); }
    public static long Latency => Session.LatencyMs;
    public static string LatencyDisplay => IsDirectHost ? "local" : $"{Latency}ms";
    public static long TimestampNow => MpWire.NowMs;
    public static long TimeOffset => Session.TimeOffsetMs;
    public static long GetSynchronizedTimestampNow => MpWire.SyncedNowMs;
    public static int ConnectedPlayersCount => Math.Max(0, (Session.Room?.Members.Count ?? 1) - 1);
    public static int AllPlayersCount => Session.Room?.Members.Count ?? 1;
    public static string RoleTag => IsRoomHost ? "[H]" : IsRoomClient ? "[C]" : "[P]";
    public static string RoleName => IsRoomHost ? "Host" : IsRoomClient ? "Client" : IsOnline ? "Public" : "Offline";
    public static Common.UI.Scene LocalScene => GameContext.Scene;
    public static bool IsMultiplayerAvailable => GameContext.HasVisitedMain;
    public static bool InStory => GameContext.InStory;
    public static bool IsGameplaySyncActive => IsConnected && !InStory;
    public static bool ShouldSkipAction => !IsGameplaySyncActive;
#if DEBUG
    public static int WorkTimeSecondOverride = 30;
#else
    public static int WorkTimeSecondOverride = 9 * 60;
#endif

    public static void RefreshInStoryCache() => GameContext.RefreshStory();
    public static bool IsValidPlayerId(string id) => Endpoint.ValidName(id);
    public static string SanitizePlayerId(string id, string fallback = null)
    {
        var result = new string((id ?? "").Where(c => c != '<' && c != '>' && !char.IsWhiteSpace(c) && !char.IsControl(c)).Take(40).ToArray());
        return result.Length == 0 ? fallback ?? Environment.MachineName : result;
    }
    public static bool Start(ROLE role = ROLE.Server, int port = -1) => EnsureAvailable()
        && (role == ROLE.Server ? MpWire.StartHost(port) : MpWire.StartClientMode());
    public static void Stop() => MpWire.Stop();
    public static bool Restart()
    {
        int port = CurrentPort;
        Stop();
        return Start(ROLE.Server, port);
    }
    public static Task<bool> ConnectToPeerAsync(string address, int port = -1, bool stop_existed_server = true) =>
        EnsureAvailable() ? MpWire.ConnectAsync(address, port, stop_existed_server) : Task.FromResult(false);
    public static void DisconnectPeer() => MpWire.DisconnectPeer();
    public static bool DisconnectClient(int uid) => MpWire.DisconnectClient(uid);
    public static bool ContinueDay() => RoomGameplay.ForceContinue(GameplayPhase.Day);
    public static bool ContinuePrep() => RoomGameplay.ForceContinue(GameplayPhase.Prep);
    public static void OnSceneTransit(Common.UI.Scene scene) => GameContext.OnSceneChanged(scene);

    private static bool EnsureAvailable()
    {
        if (!IsMultiplayerAvailable)
        {
            InGameConsole.ShowPassive(TextId.MpMainSceneRequired.Get());
            return false;
        }
        PlayerManager.Local.ReloadResourceTable();
        if (PlayerManager.Local.DataBase.IsLoaded) return true;
        InGameConsole.ShowPassive(TextId.GameResourcesNotLoaded.Get());
        return false;
    }

    public static string GetStatus()
    {
        var text = new StringBuilder();
        text.AppendLine($"{RoleTag} {PlayerManager.Local.Id} uid={Session.SelfUid}; connection={Session.Stage}");
        text.AppendLine($"Public: {Session.Players.Count}; room={Session.Room?.Name ?? "-"}; phase={RoomGameplay.Phase}/{RoomGameplay.PhaseId}");
        text.AppendLine($"Binding: {Session.Binding}; pending={Session.Pending}; ping={LatencyDisplay}");
        foreach (var player in Session.Players.Values)
            text.AppendLine($"  {(Session.Room?.Members.ContainsKey(player.Uid) == true ? "[R]" : "[P]")} {player.Name} uid={player.Uid}");
        return text.ToString();
    }
    public static string BriefStatus => !Plugin.AllPatched ? TextId.ModPatchFailure.Get()
        : !IsRunning ? "Multiplayer: Off"
        : $"MP: {RoleTag} {Session.Stage} | public {Session.Players.Count} | room {Session.Room?.Members.Count ?? 0} | {RoomGameplay.Phase} | {LatencyDisplay}";
    public static string DebugText => $"{Plugin.GameVersion}: {Plugin.ModVersion}\n{BriefStatus}";
}

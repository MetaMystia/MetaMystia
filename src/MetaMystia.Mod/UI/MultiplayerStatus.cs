using System;
using System.Linq;


using MetaMystia.Multiplayer;

namespace MetaMystia.UI;

public static class MultiplayerStatus
{
    public static string RoleTag => GameSession.IsRoomHost ? "[H]" : GameSession.IsRoomClient ? "[C]" : "[W]";
    public static string RoleName => GameSession.IsRoomHost ? "Host" : GameSession.IsRoomClient ? "Client" : GameSession.IsOnline ? "World" : "Offline";
    public static string BriefStatus => !Plugin.AllPatched ? TextId.ModPatchFailure.Get()
        : GameSession.IsConnecting ? TextId.NetworkConnecting.Get()
        : !GameSession.IsOnline ? TextId.NetworkOffline.Get()
        : TextId.NetworkStatus.Get(RoleTag, GameSession.Client.Uid, GameSession.State.World.Length, GameSession.State.MaxPlayers,
            GameSession.Room?.Id.ToString() ?? "—", GameSession.Room?.Members.Length ?? 0, RoomClock.Latency);
    public static string DebugText => $"{Plugin.GameVersion}: {Plugin.ModVersion}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {DateTimeOffset.Now}\n{BriefStatus}";
    public static string GetStatus() => BriefStatus + "\n" + string.Join("\n", GameSession.State.World.Select(p => $"{p.Uid}: {p.Name} ({p.Scene})"));
}

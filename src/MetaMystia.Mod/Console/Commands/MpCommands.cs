using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using MetaMystia.Multiplayer;
using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class MpCommands
{
    public static void Register(RootCommand root)
    {
        var mp = new Command("mp", "Multiplayer commands");
        var start = new Command("start", "Start a local server and room");
        var startPort = new Argument<int>("port", () => GameSession.ConfigPort);
        start.AddArgument(startPort);
        start.SetHandler(ctx => Start(ctx.ParseResult.GetValueForArgument(startPort)));
        var legacyStart = new Command("server", "Use /mp start");
        legacyStart.SetHandler(() => Start(GameSession.ConfigPort));
        start.AddCommand(legacyStart);
        mp.AddCommand(start);
        var stop = new Command("stop", "Stop multiplayer");
        stop.SetHandler(() => GameSession.Stop());
        mp.AddCommand(stop);
        var disconnect = new Command("disconnect", "Disconnect and close the local server");
        disconnect.SetHandler(() => GameSession.Stop());
        mp.AddCommand(disconnect);
        var restart = new Command("restart", "Restart the local server");
        restart.SetHandler(GameSession.Restart);
        mp.AddCommand(restart);
        var status = new Command("status", "Show connection and room state");
        status.SetHandler(ctx => ctx.Log(MultiplayerStatus.GetStatus()));
        mp.AddCommand(status);

        var id = new Command("id", "Set player name");
        var name = new Argument<string>("name");
        id.AddArgument(name);
        id.SetHandler(ctx =>
        {
            var value = ctx.ParseResult.GetValueForArgument(name);
            if (!PlayerIdentity.IsValid(value)) { ctx.Log(TextId.MpPlayerIdInvalid.Get()); return; }
            if (GameSession.State.World.Any(p => p.Uid != GameSession.Client?.Uid && string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase)))
            { ctx.Log(TextId.DuplicatePeerId.Get(value)); return; }
            PlayerIdentity.Name = value;
            PlayerProfile.SendProfile();
            ctx.Log(TextId.MpPlayerIdSet.Get(value));
        });
        mp.AddCommand(id);

        var connect = new Command("connect", "Connect to LAN, or use --world for a standalone server");
        var address = new Argument<string>("address");
        var port = new Argument<int?>("port", () => null);
        var world = new Option<bool>("--world", "Stay in the world without joining a room");
        connect.AddArgument(address);
        connect.AddArgument(port);
        connect.AddOption(world);
        connect.SetHandler(ctx =>
        {
            if (GameSession.IsConnecting || GameSession.HasPeers) { ctx.Log(TextId.MpConnectInProgress.Get()); return; }
            var value = ctx.ParseResult.GetValueForArgument(address);
            if (!Uri.TryCreate("tcp://" + value, UriKind.Absolute, out var endpoint))
            { ctx.Log(TextId.ConnectCommandFail.Get(value)); return; }
            var number = ctx.ParseResult.GetValueForArgument(port) ?? (endpoint.Port > 0 ? endpoint.Port : GameSession.ConfigPort);
            if (!ValidPort(number)) return;
            ctx.Log(TextId.MpConnecting.Get(endpoint.Host, number));
            GameSession.Connect(endpoint.Host, number, ctx.ParseResult.GetValueForOption(world));
        });
        mp.AddCommand(connect);

        var rooms = new Command("rooms", "List rooms in the world");
        rooms.SetHandler(ctx =>
        {
            foreach (var room in GameSession.State.Rooms)
                ctx.Log(TextId.NetworkRoomLine.Get(room.Id, room.Host, room.Count, room.MaxPlayers, (room.Joinable ? TextId.NetworkRoomOpen : TextId.NetworkRoomClosed).Get()));
        });
        mp.AddCommand(rooms);
        var create = new Command("create", "Create a room on the connected server");
        var capacity = new Argument<int>("count", () => ConfigManager.MaxPlayers.Value);
        create.AddArgument(capacity);
        create.SetHandler(ctx =>
        {
            if (!GameSession.IsOnline || GameSession.IsInRoom) { ctx.Log(TextId.NetworkWorldFirst.Get()); return; }
            var count = ctx.ParseResult.GetValueForArgument(capacity);
            if (count < 1 || count > GameSession.State.MaxPlayers) { ctx.Log($"1–{GameSession.State.MaxPlayers}"); return; }
            GameSession.CreateRoom(count);
        });
        mp.AddCommand(create);
        var join = new Command("join", "Join a room on the connected server");
        var roomId = new Argument<long>("room");
        join.AddArgument(roomId);
        join.SetHandler(ctx =>
        {
            if (!GameSession.IsOnline || GameSession.IsInRoom) { ctx.Log(TextId.NetworkWorldFirst.Get()); return; }
            GameSession.JoinRoom(ctx.ParseResult.GetValueForArgument(roomId));
        });
        mp.AddCommand(join);
        var leave = new Command("leave", "Leave the room; standalone servers retain the world connection");
        leave.SetHandler(GameSession.LeaveRoom);
        mp.AddCommand(leave);

        var kick = new Command("kick", "Remove a room member (host only)");
        var kickId = new Command("id", "Kick by name");
        var kickName = new Argument<string>("name");
        kickId.AddArgument(kickName);
        kickId.SetHandler(ctx =>
        {
            var value = ctx.ParseResult.GetValueForArgument(kickName);
            var target = PlayerManager.Peers.Values.FirstOrDefault(p => string.Equals(p.Id, value, StringComparison.OrdinalIgnoreCase));
            Kick(ctx, target?.Uid ?? 0);
        });
        var kickUid = new Command("uid", "Kick by UID");
        var uid = new Argument<int>("uid");
        kickUid.AddArgument(uid);
        kickUid.SetHandler(ctx => Kick(ctx, ctx.ParseResult.GetValueForArgument(uid)));
        kick.AddCommand(kickId);
        kick.AddCommand(kickUid);
        kick.SetHandler(ctx => ctx.Log("/mp kick id <name> | /mp kick uid <uid>"));
        mp.AddCommand(kick);

        var max = new Command("maxplayers", "View or set room capacity");
        var maximum = new Argument<int>("count", () => -1);
        max.AddArgument(maximum);
        max.SetHandler(ctx =>
        {
            int count = ctx.ParseResult.GetValueForArgument(maximum);
            if (count == -1) { ctx.Log(TextId.MpMaxPlayersCurrent.Get(GameSession.Room?.MaxPlayers ?? ConfigManager.MaxPlayers.Value)); return; }
            if (count < 1 || count > 256) { ctx.Log(TextId.NetworkLimit.Get()); return; }
            if (GameSession.IsInRoom)
            {
                if (!GameSession.IsRoomHost) { ctx.Log(TextId.MpMaxPlayersHostOnly.Get()); return; }
                GameSession.SetPlayerLimit(count);
            }
            else ConfigManager.MaxPlayers.Value = count;
        });
        mp.AddCommand(max);
        var next = new Command("continue", "Continue a waiting preparation phase");
        var phase = new Argument<string>("phase").FromAmong("day", "prep");
        next.AddArgument(phase);
        next.SetHandler(ctx =>
        {
            var value = ctx.ParseResult.GetValueForArgument(phase);
            bool success = value == "prep" && PrepSceneManager.ContinuePrep();
            ctx.Log((success ? TextId.MpContinueSuccess : TextId.MpContinueFailed).Get(value));
        });
        mp.AddCommand(next);
        var ipv6 = new Command("ipv6", "Configure IPv6 dual-stack listening");
        var enabled = new Argument<string>("action").FromAmong("enable", "disable");
        ipv6.AddArgument(enabled);
        ipv6.SetHandler(ctx =>
        {
            if (GameSession.HasPeers) { ctx.Log(TextId.MpIpv6RejectConnected.Get()); return; }
            ConfigManager.EnableIPv6.Value = ctx.ParseResult.GetValueForArgument(enabled) == "enable";
            if (GameSession.IsLanHost) GameSession.Restart();
        });
        mp.AddCommand(ipv6);
        mp.SetHandler(ctx => ctx.Log(TextId.NetworkCommands.Get()));
        root.AddCommand(mp);
        CommandRegistry.RegisterCompletions("mp", 0, "start", "stop", "restart", "status", "id", "connect", "disconnect", "rooms", "create", "join", "leave", "kick", "maxplayers", "continue", "ipv6");
        CommandRegistry.RegisterCompletions("mp continue", 0, "day", "prep");
        CommandRegistry.RegisterCompletions("mp ipv6", 0, "enable", "disable");
        CommandRegistry.RegisterCompletions("mp kick", 0, "id", "uid");
        CommandRegistry.RegisterDynamicCompletions("mp kick id", 0, () => PlayerManager.Peers.Values.Select(p => p.Id).ToArray());
        CommandRegistry.RegisterDynamicCompletions("mp kick uid", 0, () => PlayerManager.Peers.Keys.Select(p => p.ToString()).ToArray());
        CommandRegistry.RegisterDynamicCompletions("mp join", 0, () => GameSession.State.Rooms.Select(r => r.Id.ToString()).ToArray());
    }

    private static bool ValidPort(int port)
    {
        if (port is >= 1 and <= 65535) return true;
        InGameConsole.LogError(TextId.MpPortRange.Get());
        return false;
    }
    private static void Start(int port)
    {
        if (ValidPort(port)) GameSession.StartHost(port);
    }
    private static void Kick(InvocationContext ctx, int uid)
    {
        if (!GameSession.IsRoomHost) { ctx.Log(TextId.MpKickHostOnly.Get()); return; }
        if (uid == GameSession.Client.Uid) { ctx.Log(TextId.MpKickSelf.Get()); return; }
        if (!PlayerManager.Peers.ContainsKey(uid)) { ctx.Log(TextId.MpKickNotFound.Get(uid.ToString())); return; }
        GameSession.Kick(uid);
    }
}

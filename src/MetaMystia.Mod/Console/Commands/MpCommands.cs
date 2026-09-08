using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

using MetaMystia.Network;
using MetaMystia.Protocol;
using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class MpCommands
{
    public static void Register(RootCommand root)
    {
        var mpCmd = new Command("mp", "Multiplayer commands");

        // /mp start [port]
        var startCmd = new Command("start", "Start multiplayer as host");
        var startPortArg = new Argument<int>("port", () => MpManager.ConfigPort, "Server port");
        startCmd.AddArgument(startPortArg);
        startCmd.SetHandler(ctx =>
        {
            if (MpManager.IsRunning && MpManager.IsDirectHost)
            {
                ctx.Log(TextId.MpAlreadyStarted.Get(MpManager.RoleName));
                return;
            }
            if (MpManager.IsRunning && !MpManager.IsDirectHost)
            {
                ctx.Log(TextId.MpSwitchingToHost.Get());
                MpManager.Stop();
            }
            int port = ctx.ParseResult.GetValueForArgument(startPortArg);
            if (port < 1 || port > 65535)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpPortRange.Get()));
                return;
            }
            if (MpManager.Start(MpManager.ROLE.Server, port))
            {
                if (port != MpManager.DEFAULT_PORT)
                    ctx.Log(TextId.MpStartedOnPort.Get(port));
                else
                    ctx.Log(TextId.MpStartedAsHost.Get());
            }
            else if (!MpManager.IsMultiplayerAvailable)
                ctx.Log(ConsoleFormat.Err(TextId.MpMainSceneRequired.Get()));
        });

        // /mp start server (deprecated alias)
        var startServerCmd = new Command("server", "Start as host (deprecated, use '/mp start')");
        startServerCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Warn(TextId.MpStartDeprecated.Get()));
            if (MpManager.IsRunning && MpManager.IsDirectHost)
            {
                ctx.Log(TextId.MpAlreadyStarted.Get(MpManager.RoleName));
                return;
            }
            if (MpManager.IsRunning && !MpManager.IsDirectHost)
            {
                ctx.Log(TextId.MpSwitchingToHost.Get());
                MpManager.Stop();
            }
            if (MpManager.Start(MpManager.ROLE.Server))
                ctx.Log(TextId.MpStartedAsHost.Get());
            else if (!MpManager.IsMultiplayerAvailable)
                ctx.Log(ConsoleFormat.Err(TextId.MpMainSceneRequired.Get()));
        });
        startCmd.AddCommand(startServerCmd);

        mpCmd.AddCommand(startCmd);

        // /mp stop
        var stopCmd = new Command("stop", "Stop multiplayer");
        stopCmd.SetHandler(ctx =>
        {
            MpManager.Stop();
            ctx.Log(TextId.MpStopped.Get());
        });
        mpCmd.AddCommand(stopCmd);

        // /mp restart
        var restartCmd = new Command("restart", "Restart multiplayer");
        restartCmd.SetHandler(ctx =>
        {
            if (MpManager.Restart())
                ctx.Log(TextId.MpRestarted.Get());
        });
        mpCmd.AddCommand(restartCmd);

        // /mp status
        var statusCmd = new Command("status", "Show multiplayer status");
        statusCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Header("Multiplayer Status"));
            ctx.Log($"  {ConsoleFormat.Dim("Role:")} {ConsoleFormat.Cmd(MpManager.RoleName)} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("ID:")} {ConsoleFormat.Arg(MpManager.PlayerId)} {ConsoleFormat.Dim($"(uid={PlayerManager.Local.Uid})")}");
            ctx.Log($"  Connection: {MpManager.Session.Stage} | Room: {MpManager.Session.Room?.Name ?? "-"} | Phase: {RoomGameplay.Phase} | Pending: {MpManager.Session.Pending}");
            ctx.Log($"  {ConsoleFormat.Dim("Running:")} {(MpManager.IsRunning ? ConsoleFormat.Ok("Yes") : ConsoleFormat.Err("No"))} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Connected:")} {(MpManager.IsConnected ? ConsoleFormat.Ok("Yes") : ConsoleFormat.Err("No"))} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("IPv6:")} {(MpManager.EnableIPv6 ? ConsoleFormat.Ok("On") : ConsoleFormat.Dim("Off"))}");
            if (MpManager.IsConnected)
            {
                ctx.Log($"  {ConsoleFormat.Dim("Ping:")} {MpManager.LatencyDisplay} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Players:")} {MpManager.AllPlayersCount}/{MpManager.Session.Room?.Capacity} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Scene:")} {MpManager.LocalScene}");
                foreach (var kvp in PlayerManager.Peers)
                {
                    var role = kvp.Key == MpManager.Session.HostUid ? ConsoleFormat.Cmd("[S]") : ConsoleFormat.Dim("[C]");
                    ctx.Log($"    {role} {ConsoleFormat.Arg(kvp.Value.Id)} {ConsoleFormat.Dim($"uid={kvp.Key}")}");
                }
            }
            ctx.Log(ConsoleFormat.Line);
        });
        mpCmd.AddCommand(statusCmd);

        // /mp id <id>
        var idCmd = new Command("id", "Set player ID");
        var idArg = new Argument<string>("id", "New player ID");
        idCmd.AddArgument(idArg);
        idCmd.SetHandler(ctx =>
        {
            string id = ctx.ParseResult.GetValueForArgument(idArg);
            if (!MpManager.IsValidPlayerId(id))
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpPlayerIdInvalid.Get()));
                return;
            }
            MpManager.PlayerId = id;
            if (MpManager.IsOnline && !MpWire.Request(RoomOperation.Rename, name: id))
            {
                ctx.Log(TextId.MpRoomRequestUnavailable.Get());
                return;
            }
            ctx.Log(MpManager.IsOnline ? TextId.MpRoomRequestSubmitted.Get() : TextId.MpPlayerIdSet.Get(id));
        });
        mpCmd.AddCommand(idCmd);

        // /mp connect <address> [port]
        var connectCmd = new Command("connect", "Connect to a peer");
        var addressArg = new Argument<string>("address", "IP address or IP:port");
        var portArg = new Argument<int?>("port") { Arity = ArgumentArity.ZeroOrOne };
        portArg.SetDefaultValue(null);
        connectCmd.AddArgument(addressArg);
        connectCmd.AddArgument(portArg);
        connectCmd.SetHandler(ctx =>
        {
            string address = ctx.ParseResult.GetValueForArgument(addressArg);
            int? port = ctx.ParseResult.GetValueForArgument(portArg);

            if (MpManager.IsOnline)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpAlreadyOnline.Get()));
                return;
            }
            if (MpManager.IsConnecting)
            {
                ctx.Log(ConsoleFormat.Warn(TextId.MpConnectInProgress.Get()));
                return;
            }

            string host = address;
            int resolvedPort = port ?? -1;

            if (!port.HasValue)
            {
                int idx = address.LastIndexOf(':');
                if (idx > 0 && idx != address.Length - 1)
                {
                    string portStr = address[(idx + 1)..];
                    if (int.TryParse(portStr, out int parsedPort))
                    {
                        host = address[..idx];
                        resolvedPort = parsedPort;
                    }
                }
            }

            ctx.Log(TextId.MpConnecting.Get(host, resolvedPort < 0 ? MpManager.ConfigPort : resolvedPort));
            _ = ConnectAsync(host, resolvedPort, address);
        });
        mpCmd.AddCommand(connectCmd);

        // /mp disconnect
        var disconnectCmd = new Command("disconnect", "Disconnect from peer");
        disconnectCmd.SetHandler(ctx =>
        {
            if (!MpManager.IsRunning)
                ctx.Log(TextId.MpNoActiveConnection.Get());
            else
            {
                MpManager.DisconnectPeer();
                ctx.Log(TextId.MpDisconnected.Get());
            }
        });
        mpCmd.AddCommand(disconnectCmd);

        // /mp kick id <name> | /mp kick uid <uid>
        var kickCmd = new Command("kick", "Kick a player (host only)");

        var kickIdCmd = new Command("id", "Kick by player name");
        var kickNameArg = new Argument<string>("name", "Player name");
        kickIdCmd.AddArgument(kickNameArg);
        kickIdCmd.SetHandler(ctx =>
        {
            if (!MpManager.IsRoomHost) { ctx.Log(TextId.MpKickHostOnly.Get()); return; }
            if ((PlayerManager.Peers.Count == 0)) { ctx.Log(TextId.MpKickNoTarget.Get()); return; }
            string name = ctx.ParseResult.GetValueForArgument(kickNameArg);
            if (PlayerManager.Peers.Values.Count(peer => string.Equals(peer.Id, name, StringComparison.OrdinalIgnoreCase)) > 1)
            {
                ctx.Log(TextId.MpKickAmbiguous.Get());
                return;
            }
            foreach (var kvp in PlayerManager.Peers)
            {
                if (string.Equals(kvp.Value.Id, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Log(MpManager.DisconnectClient(kvp.Key)
                        ? TextId.MpRoomKickRequested.Get(kvp.Value.Id) : TextId.MpRoomRequestUnavailable.Get());
                    return;
                }
            }
            ctx.Log(ConsoleFormat.Err(TextId.MpKickNotFound.Get(name)));
        });
        kickCmd.AddCommand(kickIdCmd);

        var kickUidCmd = new Command("uid", "Kick by UID");
        var kickUidArg = new Argument<int>("uid", "Player UID");
        kickUidCmd.AddArgument(kickUidArg);
        kickUidCmd.SetHandler(ctx =>
        {
            if (!MpManager.IsRoomHost) { ctx.Log(TextId.MpKickHostOnly.Get()); return; }
            if ((PlayerManager.Peers.Count == 0)) { ctx.Log(TextId.MpKickNoTarget.Get()); return; }
            int uid = ctx.ParseResult.GetValueForArgument(kickUidArg);
            if (uid == MpManager.Session.SelfUid) { ctx.Log(TextId.MpKickSelf.Get()); return; }
            if (PlayerManager.Peers.TryGetValue(uid, out var peer))
            {
                ctx.Log(MpManager.DisconnectClient(uid)
                    ? TextId.MpRoomKickRequested.Get(peer.Id) : TextId.MpRoomRequestUnavailable.Get());
            }
            else
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpKickNotFound.Get(uid.ToString())));
            }
        });
        kickCmd.AddCommand(kickUidCmd);

        // Default: show kick usage
        kickCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.SubCmd("/mp kick id", "<name>", TextId.MpDescKickId.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp kick uid", "<uid>", TextId.MpDescKickUid.Get()));
            if (MpManager.IsRoomHost && !(PlayerManager.Peers.Count == 0))
            {
                ctx.Log(ConsoleFormat.Dim("Online: " + string.Join(", ",
                    PlayerManager.Peers.Select(p => $"{p.Value.Id}(uid={p.Key})"))));
            }
        });
        mpCmd.AddCommand(kickCmd);

        // /mp maxplayers [number]
        var maxPlayersCmd = new Command("maxplayers", "View or set max player limit");
        var maxPlayersArg = new Argument<int>("count", () => -1, "Max players (>= 2)");
        maxPlayersCmd.AddArgument(maxPlayersArg);
        maxPlayersCmd.SetHandler(ctx =>
        {
            int count = ctx.ParseResult.GetValueForArgument(maxPlayersArg);
            if (count == -1)
            {
                ctx.Log(TextId.MpMaxPlayersCurrent.Get(MpManager.Session.Room?.Capacity ?? ConfigManager.MaxPlayers.Value));
                return;
            }
            if (!MpManager.IsRoomHost && MpManager.IsConnected)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpMaxPlayersHostOnly.Get()));
                return;
            }
            if (count < 2 || count > 64)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpRoomCapacityRange.Get()));
                return;
            }
            ConfigManager.MaxPlayers.Value = count;
            if (MpManager.IsRoomHost)
                ctx.Log(MpWire.Request(RoomOperation.SetCapacity) ? TextId.MpRoomRequestSubmitted.Get() : TextId.MpRoomRequestUnavailable.Get());
            else ctx.Log(TextId.MpMaxPlayersSet.Get(count));
        });
        mpCmd.AddCommand(maxPlayersCmd);

        // /mp continue <phase>
        var continueCmd = new Command("continue", "Force continue to next phase (host only)");
        var phaseArg = new Argument<string>("phase", "Phase to continue to")
            .FromAmong("day", "prep");
        continueCmd.AddArgument(phaseArg);
        continueCmd.SetHandler(ctx =>
        {
            if (!MpManager.IsRoomHost)
            {
                ctx.Log(TextId.MpContinueHostOnly.Get());
                return;
            }
            string phase = ctx.ParseResult.GetValueForArgument(phaseArg);
            bool success = phase == "day" ? MpManager.ContinueDay() : MpManager.ContinuePrep();
            ctx.Log(success
                ? TextId.MpContinueSuccess.Get(phase)
                : TextId.MpContinueFailed.Get(phase));
        });
        mpCmd.AddCommand(continueCmd);

        // /mp ipv6 <enable|disable>
        var ipv6Cmd = new Command("ipv6", "Enable or disable IPv6 dual-stack listening");
        var ipv6ActionArg = new Argument<string>("action", "enable or disable")
            .FromAmong("enable", "disable");
        ipv6Cmd.AddArgument(ipv6ActionArg);
        ipv6Cmd.SetHandler(ctx =>
        {
            string action = ctx.ParseResult.GetValueForArgument(ipv6ActionArg);
            bool enable = action == "enable";
            if (MpManager.IsConnectedServer)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpIpv6RejectConnected.Get()));
                return;
            }
            ConfigManager.EnableIPv6.Value = enable;
            ctx.Log(enable ? TextId.MpIpv6Enabled.Get() : TextId.MpIpv6Disabled.Get());
            if (MpManager.IsRunning && MpManager.IsDirectHost)
            {
                MpManager.Restart();
                ctx.Log(TextId.MpIpv6Restarted.Get());
            }
        });
        mpCmd.AddCommand(ipv6Cmd);

        // Default handler when /mp is called without subcommand
        mpCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Header(TextId.MpHelpHeader.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp start", "[port]", TextId.MpDescStart.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp stop", null, TextId.MpDescStop.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp restart", null, TextId.MpDescRestart.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp status", null, TextId.MpDescStatus.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp id", "<id>", TextId.MpDescId.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp connect", "<addr> [port]", TextId.MpDescConnect.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp room", "list|create|join|leave", TextId.MpDescRoom.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp disconnect", null, TextId.MpDescDisconnect.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp kick", "id|uid <target>", TextId.MpDescKick.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp maxplayers", "[count]", TextId.MpDescMaxPlayers.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp continue", "<day|prep>", TextId.MpDescContinue.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp ipv6", "<enable|disable>", TextId.MpDescIpv6.Get()));
            ctx.Log(ConsoleFormat.Line);
        });

        var roomCmd = new Command("room", "Manage server rooms");
        var roomsCmd = new Command("list", "List rooms");
        roomsCmd.SetHandler(ctx =>
        {
            foreach (var room in MpManager.Session.Rooms)
                ctx.Log(TextId.MpRoomListEntry.Get(room.Id, room.Name, room.Count, room.Capacity, (room.AdmissionOpen ? TextId.MpRoomOpenLabel : TextId.MpRoomClosedLabel).Get()));
        });
        roomCmd.AddCommand(roomsCmd);

        var createCmd = new Command("create", "Create a room");
        var roomNameArg = new Argument<string>("name", () => MpManager.PlayerId);
        createCmd.AddArgument(roomNameArg);
        createCmd.SetHandler(ctx =>
        {
            if (!GameContext.CanEnterRoom) { ctx.Log(TextId.MpRoomEntryUnavailable.Get()); return; }
            ctx.Log(MpWire.Request(RoomOperation.Create, name: ctx.ParseResult.GetValueForArgument(roomNameArg))
                ? TextId.MpRoomRequestSubmitted.Get() : TextId.MpRoomRequestUnavailable.Get());
        });
        roomCmd.AddCommand(createCmd);

        var joinCmd = new Command("join", "Join a room");
        var roomIdArg = new Argument<string>("id", "Room ID from /mp room list");
        joinCmd.AddArgument(roomIdArg);
        joinCmd.SetHandler(ctx =>
        {
            if (!GameContext.CanEnterRoom) { ctx.Log(TextId.MpRoomEntryUnavailable.Get()); return; }
            if (!Guid.TryParse(ctx.ParseResult.GetValueForArgument(roomIdArg), out var id)) { ctx.Log(TextId.MpRoomIdInvalid.Get()); return; }
            ctx.Log(MpWire.Request(RoomOperation.Join, id) ? TextId.MpRoomRequestSubmitted.Get() : TextId.MpRoomRequestUnavailable.Get());
        });
        roomCmd.AddCommand(joinCmd);

        var leaveCmd = new Command("leave", "Leave room and keep public connection");
        leaveCmd.SetHandler(ctx => ctx.Log(RoomGameplay.Leave() ? TextId.MpRoomLeaving.Get() : TextId.MpRoomRequestUnavailable.Get()));
        roomCmd.AddCommand(leaveCmd);
        roomCmd.SetHandler(ctx => ctx.Log("/mp room list | create [name] | join <id> | leave"));
        mpCmd.AddCommand(roomCmd);

        root.AddCommand(mpCmd);

        CommandRegistry.RegisterCompletions("mp", 0, "start", "stop", "restart", "status", "id", "connect", "disconnect", "kick", "maxplayers", "continue", "ipv6", "room");

        CommandRegistry.RegisterCompletions("mp room", 0, "list", "create", "join", "leave");
        CommandRegistry.RegisterDynamicCompletions("mp room join", 0, () => MpManager.Session.Rooms.Where(room => room.AdmissionOpen).Select(room => room.Id.ToString()).ToArray());
        CommandRegistry.RegisterCompletions("mp continue", 0, "day", "prep");
        CommandRegistry.RegisterCompletions("mp ipv6", 0, "enable", "disable");
        CommandRegistry.RegisterCompletions("mp kick", 0, "id", "uid");
        CommandRegistry.RegisterDynamicCompletions("mp kick id", 0, () =>
            PlayerManager.Peers.Values.Select(p => p.Id).ToArray());
        CommandRegistry.RegisterDynamicCompletions("mp kick uid", 0, () =>
            PlayerManager.Peers.Keys.Select(uid => uid.ToString()).ToArray());
        CommandRegistry.RegisterHint("mp id", 0, "<player ID>");
        CommandRegistry.RegisterHint("mp connect", 0, "<IP address or IP:port>");
        CommandRegistry.RegisterHint("mp connect", 1, "<port>");
    }

    private static async Task ConnectAsync(string host, int port, string address)
    {
        if (!await MpManager.ConnectToPeerAsync(host, port))
            PluginManager.RunOnMainThread(() => InGameConsole.LogError(TextId.ConnectCommandFail.Get(address)));
    }
}

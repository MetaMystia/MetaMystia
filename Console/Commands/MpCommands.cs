using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

using MetaMystia.Network;
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
            var scope = MpManager.IsInRoom ? "Room" : MpManager.IsInPublicScope ? "Public" : "None";
            ctx.Log($"  {ConsoleFormat.Dim("Transport:")} {MpManager.Session.TransportKind} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Scope:")} {scope} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("RoomRole:")} {PlayerManager.Local.Role} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Room:")} {MpSession.FormatRoomId(PlayerManager.Local.RoomId)}");
            ctx.Log($"  {ConsoleFormat.Dim("Running:")} {(MpManager.IsRunning ? ConsoleFormat.Ok("Yes") : ConsoleFormat.Err("No"))} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Connected:")} {(MpManager.IsConnected ? ConsoleFormat.Ok("Yes") : ConsoleFormat.Err("No"))} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("IPv6:")} {(MpManager.EnableIPv6 ? ConsoleFormat.Ok("On") : ConsoleFormat.Dim("Off"))}");
            if (MpManager.IsRoomConnected)
            {
                ctx.Log($"  {ConsoleFormat.Dim("Ping:")} {MpManager.LatencyDisplay} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Players:")} {MpManager.AllPlayersCount}/{ConfigManager.MaxPlayers.Value} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Scene:")} {MpManager.LocalScene}");
                foreach (var peer in PlayerManager.RoomPeers)
                {
                    var role = peer.Uid == MpManager.Session.HostUid ? ConsoleFormat.Cmd("[S]") : ConsoleFormat.Dim("[C]");
                    ctx.Log($"    {role} {ConsoleFormat.Arg(peer.Id)} {ConsoleFormat.Dim($"uid={peer.Uid}")}");
                }
            }
            else if (MpManager.IsPublicConnected)
            {
                ctx.Log($"  {ConsoleFormat.Dim("Public lobby connected")} {ConsoleFormat.Dim("|")} {ConsoleFormat.Dim("Online:")} {MpManager.OnlinePlayersCount}");
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
            PlayerChangeIdBehavior.Send(id);
            ctx.Log(TextId.MpPlayerIdSet.Get(id));
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

            if (MpManager.IsRoomConnected)
            {
                ctx.Log(ConsoleFormat.Err(TextId.ConnectCommandConnected.Get(address)));
                return;
            }
            if (MpManager.IsConnecting)
            {
                ctx.Log(ConsoleFormat.Warn(TextId.MpConnectInProgress.Get()));
                return;
            }

            // Fire-and-forget: resolve address then connect on background thread
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

            _ = Task.Run(async () =>
            {
                bool result = await MpManager.ConnectToPeerAsync(host, resolvedPort);
                if (result)
                    InGameConsole.ShowPassiveFromAnyThread(TextId.ConnectCommandConnected.Get(address));
                else
                    InGameConsole.ShowPassiveFromAnyThread(TextId.ConnectCommandFail.Get(address));
            });
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

        // /mp join <roomId>
        var joinCmd = new Command("join", "Join a relay room from the public lobby");
        var roomIdArg = new Argument<string>("roomId", "Room ID");
        joinCmd.AddArgument(roomIdArg);
        joinCmd.SetHandler(ctx =>
        {
            string roomId = ctx.ParseResult.GetValueForArgument(roomIdArg);
            if (!MpManager.TryParseRoomId(roomId, out var parsedRoomId))
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpRoomIdInvalid.Get()));
                return;
            }
            if (MpManager.JoinRelayRoom(parsedRoomId))
                ctx.Log($"Joining room {ConsoleFormat.Arg(MpSession.FormatRoomId(parsedRoomId))}...");
            else
                ctx.Log(ConsoleFormat.Err(TextId.MpJoinRequiresRelayPublic.Get()));
        });
        mpCmd.AddCommand(joinCmd);

        // /mp create
        var createCmd = new Command("create", "Create a new relay room (host assigns random room id)");
        createCmd.SetHandler(ctx =>
        {
            if (MpManager.CreateRelayRoom())
                ctx.Log(TextId.MpCreateRequested.Get());
            else
                ctx.Log(ConsoleFormat.Err(TextId.MpJoinRequiresRelayPublic.Get()));
        });
        mpCmd.AddCommand(createCmd);

        // /mp leave
        var leaveCmd = new Command("leave", "Leave the current room");
        leaveCmd.SetHandler(ctx =>
        {
            if (MpManager.LeaveRoom())
                ctx.Log(TextId.MpLeftRoom.Get());
            else
                ctx.Log(ConsoleFormat.Err(TextId.MpNoActiveConnection.Get()));
        });
        mpCmd.AddCommand(leaveCmd);

        // /mp kick id <name> | /mp kick uid <uid>
        var kickCmd = new Command("kick", "Kick a player (host only)");

        var kickIdCmd = new Command("id", "Kick by player name");
        var kickNameArg = new Argument<string>("name", "Player name");
        kickIdCmd.AddArgument(kickNameArg);
        kickIdCmd.SetHandler(ctx =>
        {
            if (!MpManager.IsRoomHost) { ctx.Log(TextId.MpKickHostOnly.Get()); return; }
            if (!PlayerManager.RoomPeers.Any()) { ctx.Log(TextId.MpKickNoTarget.Get()); return; }
            string name = ctx.ParseResult.GetValueForArgument(kickNameArg);
            foreach (var peer in PlayerManager.RoomPeers)
            {
                if (string.Equals(peer.Id, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    MpManager.DisconnectClient(peer.Uid);
                    ctx.Log(TextId.MpKickSuccess.Get(peer.Id, peer.Uid));
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
            if (!PlayerManager.RoomPeers.Any()) { ctx.Log(TextId.MpKickNoTarget.Get()); return; }
            int uid = ctx.ParseResult.GetValueForArgument(kickUidArg);
            if (uid == PlayerManager.Local.Uid || uid == MpManager.Session.HostUid) { ctx.Log(TextId.MpKickSelf.Get()); return; }
            if (PlayerManager.TryGetRoomPeer(uid, out var peer))
            {
                MpManager.DisconnectClient(uid);
                ctx.Log(TextId.MpKickSuccess.Get(peer.Id, uid));
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
            if (MpManager.IsRoomHost && PlayerManager.RoomPeers.Any())
            {
                ctx.Log(ConsoleFormat.Dim("Online: " + string.Join(", ",
                    PlayerManager.RoomPeers.Select(p => $"{p.Id}(uid={p.Uid})"))));
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
                ctx.Log(TextId.MpMaxPlayersCurrent.Get(ConfigManager.MaxPlayers.Value));
                return;
            }
            if (!MpManager.IsRoomHost && MpManager.IsRoomConnected)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpMaxPlayersHostOnly.Get()));
                return;
            }
            if (count < 2)
            {
                ctx.Log(ConsoleFormat.Err(TextId.MpMaxPlayersRange.Get()));
                return;
            }
            ConfigManager.MaxPlayers.Value = count;
            ctx.Log(TextId.MpMaxPlayersSet.Get(count));
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
            if (MpManager.IsConnected)
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
            ctx.Log(ConsoleFormat.SubCmd("/mp disconnect", null, TextId.MpDescDisconnect.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp join", "<roomId>", TextId.MpDescJoin.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp create", null, TextId.MpDescCreate.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp leave", null, TextId.MpDescLeave.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp kick", "id|uid <target>", TextId.MpDescKick.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp maxplayers", "[count]", TextId.MpDescMaxPlayers.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp continue", "<day|prep>", TextId.MpDescContinue.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/mp ipv6", "<enable|disable>", TextId.MpDescIpv6.Get()));
            ctx.Log(ConsoleFormat.Line);
        });

        root.AddCommand(mpCmd);

        CommandRegistry.RegisterCompletions("mp", 0, "start", "stop", "restart", "status", "id", "connect", "disconnect", "join", "create", "leave", "kick", "maxplayers", "continue", "ipv6");

        CommandRegistry.RegisterCompletions("mp continue", 0, "day", "prep");
        CommandRegistry.RegisterCompletions("mp ipv6", 0, "enable", "disable");
        CommandRegistry.RegisterCompletions("mp kick", 0, "id", "uid");
        CommandRegistry.RegisterDynamicCompletions("mp kick id", 0, () =>
            PlayerManager.RoomPeers.Select(p => p.Id).ToArray());
        CommandRegistry.RegisterDynamicCompletions("mp kick uid", 0, () =>
            PlayerManager.RoomPeers.Select(p => p.Uid.ToString()).ToArray());
        CommandRegistry.RegisterHint("mp id", 0, "<player ID>");
        CommandRegistry.RegisterHint("mp connect", 0, "<IP address or IP:port>");
        CommandRegistry.RegisterHint("mp connect", 1, "<port>");
        CommandRegistry.RegisterHint("mp join", 0, "<room ID>");
    }
}

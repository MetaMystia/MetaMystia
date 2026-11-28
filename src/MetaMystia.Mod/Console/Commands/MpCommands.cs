using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class MpCommands
{
    public static void Register(RootCommand root)
    {
        var mp = new Command("mp", TextId.CmdDescMp.Get());
        var start = new Command("start", TextId.MpDescStart.Get());
        var startPort = new Argument<int>("port", () => GameSession.ConfigPort, TextId.ArgDescPort.Get());
        start.AddArgument(startPort);
        start.SetHandler(ctx => Start(ctx.ParseResult.GetValueForArgument(startPort)));
        var legacyStart = new Command("server") { IsHidden = true };
        legacyStart.SetHandler(() => Start(GameSession.ConfigPort));
        start.AddCommand(legacyStart);
        mp.AddCommand(start);
        var stop = new Command("stop", TextId.MpDescStop.Get());
        stop.SetHandler(() => GameSession.Stop());
        mp.AddCommand(stop);
        var disconnect = new Command("disconnect", TextId.MpDescDisconnect.Get());
        disconnect.SetHandler(() => GameSession.Stop());
        mp.AddCommand(disconnect);
        var restart = new Command("restart", TextId.MpDescRestart.Get());
        restart.SetHandler(GameSession.Restart);
        mp.AddCommand(restart);
        var status = new Command("status", TextId.MpDescStatus.Get());
        status.SetHandler(ctx => ctx.Log(MultiplayerStatus.GetStatus()));
        mp.AddCommand(status);

        var id = new Command("id", TextId.MpDescId.Get());
        var name = new Argument<string>("name", TextId.ArgDescPlayerName.Get());
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

        var connect = new Command("connect", TextId.MpDescConnect.Get());
        var address = new Argument<string>("address", TextId.ArgDescAddress.Get());
        var port = new Argument<int?>("port", () => null, TextId.ArgDescPort.Get());
        connect.AddArgument(address);
        connect.AddArgument(port);
        connect.SetHandler(ctx => Connect(ctx, ctx.ParseResult.GetValueForArgument(address), ctx.ParseResult.GetValueForArgument(port)));
        mp.AddCommand(connect);

        var play = new Command("play", TextId.MpDescPlay.Get());
        play.SetHandler(GameSession.ConnectOfficial);
        mp.AddCommand(play);

        var rooms = new Command("rooms", TextId.MpDescRooms.Get());
        rooms.SetHandler(ctx =>
        {
            foreach (var room in GameSession.State.Rooms)
                ctx.Log(TextId.NetworkRoomLine.Get(RoomCode.Format(room.Id), room.Host, room.Count, room.MaxPlayers, (room.Joinable ? TextId.NetworkRoomOpen : TextId.NetworkRoomClosed).Get()));
        });
        mp.AddCommand(rooms);
        var create = new Command("create", TextId.MpDescCreate.Get());
        var capacity = new Argument<int>("count", () => ConfigManager.MaxPlayers.Value, TextId.ArgDescPlayerCount.Get());
        create.AddArgument(capacity);
        create.SetHandler(ctx =>
        {
            if (!GameSession.IsOnline) { ctx.Log(TextId.NetworkWorldFirst.Get()); return; }
            if (GameSession.IsInRoom) { ctx.Log(TextId.NetworkLeaveRoomFirst.Get()); return; }
            var count = ctx.ParseResult.GetValueForArgument(capacity);
            if (count < 1 || count > GameSession.State.MaxPlayers) { ctx.Log($"1–{GameSession.State.MaxPlayers}"); return; }
            GameSession.CreateRoom(count);
        });
        mp.AddCommand(create);
        var join = new Command("join", TextId.MpDescJoin.Get());
        var roomId = new Argument<string>("room", TextId.ArgDescRoomCode.Get())
            .AddCompletions(_ => GameSession.State.Rooms.Select(r => RoomCode.Format(r.Id)));
        join.AddArgument(roomId);
        join.SetHandler(ctx =>
        {
            if (!GameSession.IsOnline) { ctx.Log(TextId.NetworkWorldFirst.Get()); return; }
            if (GameSession.IsInRoom) { ctx.Log(TextId.NetworkLeaveRoomFirst.Get()); return; }
            if (!RoomCode.TryParse(ctx.ParseResult.GetValueForArgument(roomId), out var code))
            { ctx.Log(TextId.NetworkRoomCodeInvalid.Get()); return; }
            GameSession.JoinRoom(code);
        });
        mp.AddCommand(join);
        var leave = new Command("leave", TextId.MpDescLeave.Get());
        leave.SetHandler(() => GameSession.LeaveRoom());
        mp.AddCommand(leave);

        var kick = new Command("kick", TextId.MpDescKick.Get());
        var kickId = new Command("id", TextId.MpDescKickId.Get());
        var kickName = new Argument<string>("name", TextId.ArgDescPlayerName.Get())
            .AddCompletions(_ => PlayerManager.Peers.Values.Select(p => p.Id));
        kickId.AddArgument(kickName);
        kickId.SetHandler(ctx =>
        {
            var value = ctx.ParseResult.GetValueForArgument(kickName);
            var target = PlayerManager.Peers.Values.FirstOrDefault(p => string.Equals(p.Id, value, StringComparison.OrdinalIgnoreCase));
            Kick(ctx, target?.Uid ?? 0);
        });
        var kickUid = new Command("uid", TextId.MpDescKickUid.Get());
        var uid = new Argument<int>("uid", TextId.ArgDescUid.Get())
            .AddCompletions(_ => PlayerManager.Peers.Keys.Select(p => p.ToString()));
        kickUid.AddArgument(uid);
        kickUid.SetHandler(ctx => Kick(ctx, ctx.ParseResult.GetValueForArgument(uid)));
        kick.AddCommand(kickId);
        kick.AddCommand(kickUid);
        mp.AddCommand(kick);

        var max = new Command("maxplayers", TextId.MpDescMaxPlayers.Get());
        var maximum = new Argument<int>("count", () => -1, TextId.ArgDescPlayerCount.Get());
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
            else
            {
                ConfigManager.MaxPlayers.Value = count;
                ctx.Log(TextId.MpMaxPlayersSet.Get(count));
            }
        });
        mp.AddCommand(max);
        var next = new Command("continue", TextId.MpDescContinue.Get());
        var phase = new Argument<string>("phase").FromAmong("day", "prep");
        next.AddArgument(phase);
        next.SetHandler(ctx =>
        {
            var value = ctx.ParseResult.GetValueForArgument(phase);
            bool success = value == "prep" && PrepSceneManager.ContinuePrep();
            ctx.Log((success ? TextId.MpContinueSuccess : TextId.MpContinueFailed).Get(value));
        });
        mp.AddCommand(next);
        var ipv6 = new Command("ipv6", TextId.MpDescIpv6.Get());
        var enabled = new Argument<string>("action").FromAmong("enable", "disable");
        ipv6.AddArgument(enabled);
        ipv6.SetHandler(ctx =>
        {
            if (GameSession.HasRoomPeers) { ctx.Log(TextId.MpIpv6RejectConnected.Get()); return; }
            ConfigManager.EnableIPv6.Value = ctx.ParseResult.GetValueForArgument(enabled) == "enable";
            if (GameSession.HasLocalServer) GameSession.Restart();
        });
        mp.AddCommand(ipv6);

        var versus = new Command("versus", TextId.MpDescVersus.Get());
        var pace = new Argument<string>("pace", () => "normal", TextId.ArgDescVersusTempo.Get()).FromAmong("slow", "normal", "fast");
        versus.AddArgument(pace);
        versus.SetHandler(ctx => Versus(ctx, ctx.ParseResult.GetValueForArgument(pace)));
        var cancel = new Command("cancel", TextId.MpDescVersusCancel.Get());
        cancel.SetHandler(ctx =>
        {
            if (!GameSession.IsRoomHost) { ctx.Log(ConsoleFormat.Err(TextId.VersusHostOnly.Get())); return; }
            if (!VersusRoster.Open) { ctx.Log(TextId.VersusNoInvite.Get()); return; }
            VersusManager.CloseInvite();
            ctx.Log(TextId.VersusCanceled.Get());
        });
        versus.AddCommand(cancel);
        mp.AddCommand(versus);
        var team = new Command("team", TextId.MpDescTeam.Get());
        var teamTarget = new Argument<string>("team", () => null, TextId.ArgDescVersusTeam.Get())
            .AddCompletions(_ => TeamNames.Append("shuffle").Concat(GameSession.Room?.Members.Select(member => member.Name) ?? []));
        var teamValue = new Argument<string>("value", () => null, TextId.ArgDescVersusTeam.Get()).AddCompletions(TeamNames);
        team.AddArgument(teamTarget);
        team.AddArgument(teamValue);
        team.SetHandler(ctx => Team(ctx, ctx.ParseResult.GetValueForArgument(teamTarget), ctx.ParseResult.GetValueForArgument(teamValue)));
        mp.AddCommand(team);
        root.AddCommand(mp);
    }

    private static void Versus(InvocationContext ctx, string pace)
    {
        if (!GameSession.IsRoomHost) { ctx.Log(ConsoleFormat.Err(TextId.VersusHostOnly.Get())); return; }
        if (GameFlow.Stage != GameStage.Day || DayDestinationManager.IsEntering)
        { ctx.Log(ConsoleFormat.Err(TextId.VersusDayRequired.Get())); return; }
        VersusManager.OpenInvite(pace switch { "slow" => VersusTempo.Slow, "fast" => VersusTempo.Fast, _ => VersusTempo.Normal });
    }

    private static readonly string[] TeamNames = ["red", "blue", "watch", "none"];

    private static VersusTeam? ParseTeam(string value) => value?.ToLowerInvariant() switch
    {
        "red" => VersusTeam.Red,
        "blue" => VersusTeam.Blue,
        "watch" => VersusTeam.Watch,
        "none" => VersusTeam.None,
        _ => null,
    };

    /// <summary>无参数列出队伍；<c>red|blue|watch|none</c> 为自己选队；房主可 <c>shuffle</c> 或 <c>名字 队伍</c>。</summary>
    private static void Team(InvocationContext ctx, string target, string value)
    {
        if (!GameSession.IsInRoom) { ctx.Log(TextId.NetworkWorldFirst.Get()); return; }
        if (target == null)
        {
            foreach (var member in GameSession.Room.Members)
            {
                var status = VersusRoster.MissingDlc3.Contains(member.Uid) ? TextId.VersusStatusNoDlc3
                    : VersusRoster.Confirmed.Contains(member.Uid) ? TextId.VersusStatusChosen : TextId.VersusStatusPending;
                ctx.Log(TextId.VersusRosterLine.Get(LiveModeManager.GetDisplayName(member.Uid, member.Name),
                    VersusManager.TeamName(VersusRoster.TeamOf(member.Uid)), status.Get()));
            }
            VersusManager.OpenMenu();
            return;
        }
        if (value == null && ParseTeam(target) is { } own)
        {
            if (!VersusRoster.Open) { ctx.Log(TextId.VersusNoInvite.Get()); return; }
            VersusManager.Choose(own);
            return;
        }
        if (!GameSession.IsRoomHost) { ctx.Log(ConsoleFormat.Err(TextId.VersusHostOnly.Get())); return; }
        if (value == null && target.Equals("shuffle", StringComparison.OrdinalIgnoreCase))
        {
            VersusManager.Shuffle();
            return;
        }
        var player = GameSession.Room.Members.FirstOrDefault(member => string.Equals(member.Name, target, StringComparison.OrdinalIgnoreCase));
        if (player == null) { ctx.Log(ConsoleFormat.Err(TextId.MpKickNotFound.Get(target))); return; }
        if (ParseTeam(value) is not { } team || team == VersusTeam.None) { ctx.Log(ConsoleFormat.Err(TextId.ArgDescVersusTeam.Get())); return; }
        VersusManager.SetTeam(player.Uid, team);
    }

    private static void Connect(InvocationContext ctx, string address, int? port)
    {
        if (GameSession.IsConnecting) { ctx.Log(TextId.MpConnectInProgress.Get()); return; }
        if (GameSession.IsOnline) { ctx.Log(TextId.MpAlreadyConnected.Get()); return; }
        if (!Uri.TryCreate("tcp://" + address, UriKind.Absolute, out var endpoint))
        { ctx.Log(TextId.ConnectCommandFail.Get(address)); return; }
        int number = port ?? (endpoint.Port > 0 ? endpoint.Port : GameSession.ConfigPort);
        if (!ValidPort(number)) return;
        ctx.Log(TextId.MpConnecting.Get(endpoint.Host, number));
        GameSession.Connect(endpoint.Host, number);
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

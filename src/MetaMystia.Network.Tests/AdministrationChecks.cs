using MetaMystia.Hosting;
using MetaMystia.Network;

static partial class Checks
{
    static async Task Administration()
    {
        await using var server = new Server(new()
        {
            MaxPlayers = 4, Messages = GameMessageRules.Create(), LogChat = true,
            ChatFilter = new() { Enabled = true, Words = ["blocked"] }
        });
        var logs = new System.Collections.Concurrent.ConcurrentQueue<ServerLogEntry>();
        server.Logged += logs.Enqueue;
        await server.StartAsync();
        var host = await Connect(server, "admin-host");
        var guest = await Connect(server, "admin-guest");
        var outside = await Connect(server, "admin-world");
        int guestUid = guest.Uid;
        await Pump(host.CreateRoomAsync(4));
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        var status = await server.GetStatusAsync();
        Assert(status.Players.Single(p => p.Uid == guestUid).Room == host.State.Room!.Id
            && status.Players.Single(p => p.Uid == outside.Uid).Room == 0, "管理快照包含真实玩家房间与大厅状态");
        Assert(await server.ManageAsync(ServerCommand.MaxPlayers, 2) == NetworkErrorCode.None, "管理员可降低服务器上限");
        await Until(() => host.State.MaxPlayers == 2);
        Assert(host.IsConnected && guest.IsConnected && outside.IsConnected && (await server.GetStatusAsync()).Players.Length == 3,
            "降低上限发布快照但保留全部在线玩家");
        var newcomer = new Client(); clients.Add(newcomer);
        Assert(await Failure(newcomer.ConnectAsync(server.Endpoint, Player("admin-new"))) == "ServerFull", "降低上限后拒绝新连接");
        Assert(await server.ManageAsync(ServerCommand.MaxPlayers, 0) == NetworkErrorCode.InvalidLimit
            && await server.ManageAsync(ServerCommand.MaxPlayers, 257) == NetworkErrorCode.InvalidLimit
            && (await server.GetStatusAsync()).MaxPlayers == 2, "非法上限不改变状态");
        Assert(await server.ManageAsync(ServerCommand.Leave, outside.Uid) == NetworkErrorCode.NotInRoom
            && await server.ManageAsync(ServerCommand.Kick, int.MaxValue) == NetworkErrorCode.PlayerMissing, "管理目标不存在或已在大厅时返回明确错误");
        Assert(await server.ManageAsync(ServerCommand.Leave, guestUid) == NetworkErrorCode.None, "管理员移出房间客人");
        await Until(() => guest.State.Room == null && host.State.Room!.Members.Length == 1);
        Assert(guest.IsConnected && host.IsConnected, "移出客人保留双方连接和房主房间");
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        Assert(await server.ManageAsync(ServerCommand.Leave, host.Uid) == NetworkErrorCode.None, "管理员移出房主");
        await Until(() => host.State.Room == null && guest.State.Room == null);
        Assert(host.IsConnected && guest.IsConnected && (await server.GetStatusAsync()).Rooms.Length == 0,
            "移出房主解散房间且成员回大厅不掉线");
        await server.ManageAsync(ServerCommand.MaxPlayers, 4);
        await Pump(host.CreateRoomAsync(4)); await Pump(host.SetJoinableAsync(true)); await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        NetworkErrorCode? end = null;
        host.ConnectionEnded += reason => end = reason.Code;
        int hostUid = host.Uid;
        Assert(await server.ManageAsync(ServerCommand.Kick, hostUid) == NetworkErrorCode.None, "管理员踢出房主");
        await Until(() => end != null && guest.State.Room == null);
        Assert(end == NetworkErrorCode.KickedByServer && guest.IsConnected
            && (await server.GetStatusAsync()).Players.All(p => p.Uid != hostUid), "踢人返回专用原因、释放名额并清理房间");
        Assert(logs.Count(e => e.Message.Contains($"玩家下线 uid={hostUid} ")) == 1, "踢人及连接回调只记录一次下线");

        var output = new List<string>();
        void Write(ServerLogLevel level, string message) => output.Add(message);
        foreach (var line in new[] { "", "  ", "help", "/players", "rooms", "maxplayers", "kick", "kick -1", "leave text", "maxplayers 257", "stop extra", "unknown" })
            Assert(await ServerCommands.Execute(line, server, Write), "命令不意外停止服务端：" + line);
        await ServerCommands.Execute("maxplayers 8", server, Write);
        Assert((await server.GetStatusAsync()).MaxPlayers == 8 && output.Any(x => x.Contains("uid=")), "实际命令解析可查询玩家并修改上限");
        Assert(!await ServerCommands.Execute("stop", server, Write), "仅完整 stop 命令请求停止");

        int accepted = 0, rejected = 0;
        guest.MessageReceived += _ => accepted++;
        guest.ChatRejected += _ => rejected++;
        string chat = "你好\n[INFO] forged\u001b[31m";
        guest.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = chat }));
        await Until(() => accepted == 1);
        guest.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = "blocked-secret" }));
        await Until(() => rejected == 1);
        var chatLog = logs.Single(e => e.Level == ServerLogLevel.Chat);
        Assert(chatLog.Message.Contains("你好") && !chatLog.Message.Contains('\n') && !chatLog.Message.Contains('\u001b'), "聊天日志保留中文并转义换行及终端控制字符");
        Assert(logs.All(e => !e.Message.Contains("blocked-secret")), "拒绝的敏感聊天原文不写入日志");
        guest.SetProfile("admin-renamed", new(), PlayerScene.Day, GameStage.Day);
        await Until(() => logs.Any(e => e.Message.Contains("玩家改名")));
        Assert(logs.Any(e => e.Message.Contains("玩家上线")) && logs.Any(e => e.Message.Contains("房间解散"))
            && logs.Any(e => e.Message.Contains("管理员操作")), "连接、改名、房间及管理操作都有日志");
        guest.Disconnect(); outside.Disconnect();
        await server.StopAsync();
        Assert(await server.ManageAsync(ServerCommand.MaxPlayers, 4) == NetworkErrorCode.ServerStopped, "关闭后管理请求立即结束");

        await using var quiet = new Server(new() { Messages = GameMessageRules.Create() });
        var quietLogs = new System.Collections.Concurrent.ConcurrentQueue<ServerLogEntry>();
        quiet.Logged += quietLogs.Enqueue;
        await quiet.StartAsync();
        var quietClient = await Connect(quiet, "quiet");
        bool delivered = false;
        quietClient.MessageReceived += _ => delivered = true;
        quietClient.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = "private-text" }));
        await Until(() => delivered);
        Assert(quietLogs.All(e => e.Level != ServerLogLevel.Chat && !e.Message.Contains("private-text")), "默认关闭聊天正文日志");
        quietClient.Disconnect();
    }
}

using System.Collections.Concurrent;

using MetaMystia.Network;

static partial class Checks
{
    static async Task PresenceLogs()
    {
        await using var server = new Server(new() { MaxPlayers = 3 });
        var logs = new ConcurrentQueue<ServerLogEntry>();
        server.Logged += logs.Enqueue;
        await server.StartAsync();
        var host = await Connect(server, "presence-host");
        var guest = await Connect(server, "presence-guest");
        int hostUid = host.Uid, guestUid = guest.Uid;
        await Pump(host.CreateRoomAsync(3));
        await Pump(host.SetJoinableAsync(true));
        var room = host.State.Room!.Id;
        await Pump(host.SetJoinableAsync(false));
        Assert(await Failure(guest.JoinRoomAsync(room)) == "JoinClosed", "关闭加入时拒绝客人");
        Assert(logs.Any(e => e.Level == ServerLogLevel.Warning && e.Message.Contains($"(UID={guestUid})无法加入房间 {RoomCode.Format(room)}")
            && e.Message.Contains("房间已关闭加入（JoinClosed）")), "入房失败记录实际玩家、房间和中文原因");
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(room));
        Assert(logs.Count(e => e.Message.Contains("创建了房间")) == 1
            && logs.All(e => !e.Message.Contains("结果=None") && !e.Message.Contains("command=")),
            "创建房间只记录一次结果，不再输出内部命令回执");
        string guestJoin = $"[房间] 玩家\"presence-guest\"(UID={guestUid})加入了房间 {RoomCode.Format(room)}";
        string guestLeave = $"[房间] 玩家\"presence-guest\"(UID={guestUid})已离开房间 {RoomCode.Format(room)}。";
        Assert(logs.Count(e => e.Message.StartsWith($"[玩家] 玩家\"presence-guest\"(UID={guestUid})已连接服务器")) == 1
            && logs.Count(e => e.Message.StartsWith(guestJoin)) == 1, "上线与入房日志各一次，包含名字与身份");
        guest.LeaveRoom();
        await Until(() => host.State.Room!.Members.Length == 1);
        Assert(logs.Count(e => e.Message == guestLeave) == 1
            && logs.All(e => !e.Message.StartsWith($"[玩家] 玩家\"presence-guest\"(UID={guestUid})已下线")), "主动退房只记录退房，不误报下线");
        await Pump(guest.JoinRoomAsync(room));
        host.LeaveRoom();
        await Until(() => guest.State.Room == null);
        Assert(logs.Count(e => e.Message == guestLeave) == 2
            && logs.Count(e => e.Message.StartsWith($"[房间] 玩家\"presence-host\"(UID={hostUid})已离开房间 {RoomCode.Format(room)}")) == 1,
            "房主解散房间为每位成员各记录一次退房");
        await Pump(host.CreateRoomAsync(3));
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        await server.StopAsync();
        Assert(logs.Count(e => e.Message.StartsWith($"[玩家] 玩家\"presence-host\"(UID={hostUid})已下线")) == 1
            && logs.Count(e => e.Message.StartsWith($"[玩家] 玩家\"presence-guest\"(UID={guestUid})已下线")) == 1,
            "关服为全部在线玩家记录一次下线，关闭回调不重复记录");
        Assert(logs.Count(e => e.Message.Contains("已离开房间 ")) == 5, "关服补齐仍在房间中的成员退房日志");

        await using var lan = new LanSession(maxPlayers: 2);
        var lanLogs = new ConcurrentQueue<ServerLogEntry>();
        lan.Server.Logged += lanLogs.Enqueue;
        clients.Add(lan.Client);
        await Pump(lan.StartAsync(Player("presence-lan-host")));
        await Pump(lan.Client.SetJoinableAsync(true));
        var lanGuest = await Connect(lan.Server, "presence-lan-guest");
        int lanGuestUid = lanGuest.Uid;
        lanGuest.LeaveRoom();
        await WaitCount(lan.Server, 1);
        Assert(lanLogs.Count(e => e.Message.Contains("已离开房间 ") && e.Message.Contains($"(UID={lanGuestUid})")) == 1
            && lanLogs.Count(e => e.Message.StartsWith($"[玩家] 玩家\"presence-lan-guest\"(UID={lanGuestUid})已下线")) == 1,
            "局域网退房断线各记录一次退房与下线");
    }
}

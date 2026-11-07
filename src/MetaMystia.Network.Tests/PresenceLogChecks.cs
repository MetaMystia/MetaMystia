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
        await Pump(guest.JoinRoomAsync(room));
        string guestJoin = $"玩家加入房间 room={RoomCode.Format(room)} uid={guestUid} name=\"presence-guest\"";
        string guestLeave = $"玩家离开房间 room={RoomCode.Format(room)} uid={guestUid} name=\"presence-guest\"";
        Assert(logs.Count(e => e.Message.StartsWith($"玩家上线 uid={guestUid} ")) == 1
            && logs.Count(e => e.Message.StartsWith(guestJoin)) == 1, "上线与入房日志各一次，包含名字与身份");
        guest.LeaveRoom();
        await Until(() => host.State.Room!.Members.Length == 1);
        Assert(logs.Count(e => e.Message == guestLeave) == 1
            && logs.All(e => !e.Message.StartsWith($"玩家下线 uid={guestUid} ")), "主动退房只记录退房，不误报下线");
        await Pump(guest.JoinRoomAsync(room));
        host.LeaveRoom();
        await Until(() => guest.State.Room == null);
        Assert(logs.Count(e => e.Message == guestLeave) == 2
            && logs.Count(e => e.Message.StartsWith($"玩家离开房间 room={RoomCode.Format(room)} uid={hostUid} ")) == 1,
            "房主解散房间为每位成员各记录一次退房");
        await Pump(host.CreateRoomAsync(3));
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        await server.StopAsync();
        Assert(logs.Count(e => e.Message.StartsWith($"玩家下线 uid={hostUid} ")) == 1
            && logs.Count(e => e.Message.StartsWith($"玩家下线 uid={guestUid} ")) == 1,
            "关服为全部在线玩家记录一次下线，关闭回调不重复记录");
        Assert(logs.Count(e => e.Message.StartsWith("玩家离开房间 ")) == 5, "关服补齐仍在房间中的成员退房日志");

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
        Assert(lanLogs.Count(e => e.Message.StartsWith("玩家离开房间 ") && e.Message.Contains($" uid={lanGuestUid} ")) == 1
            && lanLogs.Count(e => e.Message.StartsWith($"玩家下线 uid={lanGuestUid} ")) == 1,
            "局域网退房断线各记录一次退房与下线");
    }
}

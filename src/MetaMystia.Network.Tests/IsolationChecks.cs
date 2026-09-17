using Common.UI;

using MetaMystia;
using MetaMystia.Network;

static partial class Checks
{
    static async Task Isolation()
    {
        await using var server = new Server(new() { MaxPlayers = 8, Messages = GameMessageRules.Create() });
        await server.StartAsync();
        var host = await Connect(server, "room-host");
        var guest = await Connect(server, "room-guest");
        var outside = await Connect(server, "world-player");
        var otherHost = await Connect(server, "other-host");
        var otherGuest = await Connect(server, "other-guest");
        await Pump(host.CreateRoomAsync(3));
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        await Pump(otherHost.CreateRoomAsync(2));
        await Pump(otherHost.SetJoinableAsync(true));
        await Pump(otherGuest.JoinRoomAsync(otherHost.State.Room!.Id));
        host.SendMotion(new() { X = 17 });
        await Until(() => outside.State.World.Single(p => p.Uid == host.Uid).Motion.X == 17);
        Assert(outside.State.World.Single(p => p.Uid == host.Uid).HasMotion, "白天已组房玩家仍向世界共享运动");

        await Pump(host.SetJoinableAsync(false));
        Assert(await Failure(outside.JoinRoomAsync(host.State.Room!.Id)) == "JoinClosed" && outside.IsConnected,
            "关闭确认后拒绝新成员，世界连接保留");
        host.SetProfile("room-host", new(), Scene.WorkScene, GameStage.Work);
        guest.SetProfile("room-guest", new(), Scene.WorkScene, GameStage.Work);
        otherHost.SetProfile("other-host", new(), Scene.WorkScene, GameStage.Work);
        otherGuest.SetProfile("other-guest", new(), Scene.WorkScene, GameStage.Work);
        await Until(() => guest.State.World.Single(p => p.Uid == host.Uid).Scene == Scene.WorkScene);
        host.SendMotion(new() { X = 987 });
        await Until(() => guest.State.World.Single(p => p.Uid == host.Uid).Motion.X == 987);
        var late = await Connect(server, "late-world");
        foreach (var viewer in new[] { outside, otherHost, otherGuest, late })
        {
            var player = viewer.State.World.Single(p => p.Uid == host.Uid);
            Assert(!player.HasMotion && player.Motion.X == 0, "夜间运动及新快照对房外和其他房间不可见：" + viewer.Uid);
        }
        outside.SendMotion(new() { X = 321 });
        await Until(() => late.State.World.Single(p => p.Uid == outside.Uid).Motion.X == 321);
        Assert(!host.State.World.Single(p => p.Uid == outside.Uid).HasMotion, "白天运动不会进入夜间房间视图");
        await Pump(host.SetRoomPlayerLimitAsync(2));
        Assert(outside.State.World.Single(p => p.Uid == host.Uid).Motion.X == 0, "管理快照不能补发隐藏的夜间位置");

        var requests = new List<ReceivedMessage>();
        var results = new List<ReceivedMessage>();
        host.MessageReceived += requests.Add;
        guest.MessageReceived += results.Add;
        guest.SendToHost((ushort)GameMessage.ServeSellable, [1], 42);
        await Until(() => requests.Count == 1);
        var context = requests[0].Context;
        Assert(host.IsCurrent(context), "应用延迟回调可核对双方入房身份");
        guest.SendToRoom((ushort)GameMessage.ServeSellable, [2]);
        guest.SendToHost((ushort)GameMessage.Ping, [3]);
        await Until(() => requests.Count == 2);
        Assert(requests.All(m => m.Body[0] != 2), "同一玩法类型允许客人请求但不允许客人伪造广播结果");
        host.SendToRoom((ushort)GameMessage.ServeSellable, [4], 42);
        await Until(() => results.Count == 1);
        Assert(results[0].Context.Sender == host.Uid && results[0].Context.Request == 42,
            "裁定广播来源是房主，原请求者也收到结果");
        Assert(await Failure(guest.KickAsync(host.Uid)) == "HostOnly", "客人不能踢人");
        await Pump(host.KickAsync(guest.Uid));
        await Until(() => guest.State.Room == null);
        Assert(guest.IsConnected && !host.IsCurrent(context), "无头踢人只退出房间，旧玩法回调失效");
        await Pump(host.SetJoinableAsync(true));
        guest.SetProfile("room-guest", new(), Scene.MainScene, GameStage.MainMenu);
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        Assert(!host.IsCurrent(context), "重新进入同房间也不能恢复旧回调身份");

        guest.SetProfile("room-guest", new(), Scene.WorkScene, GameStage.Work);
        host.SetProfile("room-host", new(), Scene.DayScene, GameStage.Day);
        host.SendMotion(new() { X = 61 });
        await Until(() => outside.State.World.Single(p => p.Uid == host.Uid).Motion.X == 61);
        Assert(!guest.State.World.Single(p => p.Uid == host.Uid).HasMotion, "同房间也按各自昼夜隔离");
        var copy = guest.State;
        copy.Room!.Members[0].Resources!.PackIds[0] = "changed";
        Assert(guest.State.Room!.Members[0].Resources!.PackIds[0] != "changed", "快速状态副本隔离嵌套资源数组");
        otherHost.SendMotion(new() { X = 88 });
        await Until(() => otherGuest.State.World.Single(p => p.Uid == otherHost.Uid).HasMotion);
        otherGuest.LeaveRoom();
        Assert(!otherGuest.State.World.Single(p => p.Uid == otherHost.Uid).HasMotion,
            "本地退房立即清除旧房间运动，无需等待服务器通知");
        outside.SetProfile("world-player", new(), Scene.WorkScene, GameStage.Work);
        Assert(!outside.State.World.Single(p => p.Uid == host.Uid).HasMotion,
            "本地切入夜间立即隐藏白天位置");
        await Pump(host.SetJoinableAsync(false));
        Assert(!outside.State.World.Single(p => p.Uid == host.Uid).HasMotion,
            "切场景期间的管理快照不能恢复旧世界位置");
        var dupe = new Client(); clients.Add(dupe);
        Assert(await Failure(dupe.ConnectAsync(server.Endpoint, Player("room-host"))) == "DuplicateName", "重复名称在服务器登记时拒绝");
        foreach (var client in new[] { host, guest, outside, otherHost, otherGuest, late }) client.Disconnect();
    }
}

using System.Net;
using System.Net.Sockets;

using MetaMystia;
using MetaMystia.Network;

static partial class Checks
{
    static async Task Lifecycle()
    {
        await using var server = new Server(new() { MaxPlayers = 3, Messages = rules }); await server.StartAsync();
        var a = await Connect(server, "lifecycle");
        var b = await Connect(server, "snapshot-source");
        a.DispatchPending(); clients.Remove(a);
        await Pump(b.CreateRoomAsync());
        await Until(() => a.PendingFrames > 0);
        a.SendMotion(new() { X = 123, Map = MapLabel.Home });
        a.SetProfile("new-name", new() { NetSkinName = "new-skin" }, Common.UI.Scene.WorkScene, GameStage.Work);
        a.DispatchPending(); clients.Add(a);
        var local = a.State.World.Single(p => p.Uid == a.Uid);
        Assert(!local.HasMotion && local.Motion.X == 0 && local.Name == "new-name" && local.Skin.NetSkinName == "new-skin" && local.Scene == Common.UI.Scene.WorkScene,
            "切场景清空旧运动，旧快照不能恢复运动或覆盖新资料");
        b.Disconnect(); await WaitCount(server, 1);
        using (var cancel = new CancellationTokenSource(25))
        {
            var task = a.CreateRoomAsync(token: cancel.Token);
            await Task.Delay(100);
            Assert(await Failure(task) == "Cancelled" && a.State.Room == null, "建房迟到成功同样撤销");
        }
        Assert((await server.GetSnapshotAsync()).Rooms.Length == 0, "撤销建房删除实际房间");
        a.OperationTimeout = TimeSpan.FromMilliseconds(50);
        var timeout = a.CreateRoomAsync();
        await Task.Delay(200);
        Assert(await Failure(timeout) == "Timeout" && !a.IsConnected, "撤销无法确认时断线清理");
        await WaitCount(server, 0);
        a.DispatchPending();
        a.OperationTimeout = TimeSpan.FromSeconds(2);
        await Pump(a.ConnectAsync(server.Endpoint, Player("reconnected")));
        Assert(a.IsConnected && a.State.Room == null, "同一 Client 重连不继承旧连接或入房回调");
        var request = new TaskCompletionSource<ReceivedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += m => { request.TrySetResult(m); throw new Exception("server application"); };
        a.SendToServer(5, [1], 9); await Pump(request.Task);
        Assert((await request.Task).Context.Sender == a.Uid && (await request.Task).Context.Request == 9, "给服务器路由保持真实来源和请求编号");
        var slow = await Connect(server, "slow"); clients.Remove(slow);
        for (int batch = 0; batch < 35; batch++)
        {
            for (int i = 0; i < 10; i++) a.SendToWorld(1, [1]);
            a.DispatchPending(); await Task.Delay(4);
        }
        await WaitCount(server, 1);
        Assert(a.IsConnected && !slow.IsConnected, "接收队列满只结束落后玩家，其他连接继续");
        slow.DispatchPending(); slow.Dispose();
        a.Disconnect(); await WaitCount(server, 0);
        await using var limited = new Server(new() { MaxPlayers = 1, Timeout = TimeSpan.FromMilliseconds(500) });
        await limited.StartAsync();
        using var reserved = new TcpClient(); await reserved.ConnectAsync(limited.Endpoint.Address, limited.Endpoint.Port);
        var denied = new Client(); clients.Add(denied);
        Assert(await Failure(denied.ConnectAsync(limited.Endpoint, Player("reserved"))) == "ServerFull", "未完成握手先占名额");
        await Task.Delay(800);
        var admitted = await Connect(limited, "released");
        Assert(admitted.IsConnected, "握手超时归还名额"); admitted.Disconnect();
        await using var solo = new LanSession(maxPlayers: 1); clients.Add(solo.Client);
        await Pump(solo.StartAsync(Player("solo")));
        Assert(solo.Client.State.Room!.MaxPlayers == 1, "局域网容量为一也能建房");
    }

    static async Task BadFrames(Server server, Client survivor)
    {
        int before = (await server.GetSnapshotAsync()).World.Length;
        using (var raw = new TcpClient())
        {
            await raw.ConnectAsync(server.Endpoint.Address, server.Endpoint.Port);
            var hello = Protocol.Encode(new(Kind.Hello, Protocol.Hello(Versions.Current, Player("fragment"), "")));
            foreach (var value in hello) await raw.GetStream().WriteAsync(new byte[] { value });
            var welcome = await Read(raw); Assert(welcome.Kind == Kind.Welcome, "逐字节分割帧头和正文完成握手");
            var received = new List<ReceivedMessage>(); survivor.MessageReceived += received.Add;
            var frames = Enumerable.Range(0, 60).SelectMany(i => Protocol.Encode(new(Kind.Data, BitConverter.GetBytes(i), Sender: 9999, Type: 1, Route: Route.World))).ToArray();
            await raw.GetStream().WriteAsync(frames);
            await Until(() => received.Count == 60);
            Assert(received.Select(m => BitConverter.ToInt32(m.Body)).SequenceEqual(Enumerable.Range(0, 60)) && received.All(m => m.Context.Sender == welcome.Sender), "粘连多帧有序，服务端覆盖伪造来源 UID");
        }
        await WaitCount(server, before);
        foreach (var bad in new byte[][] { [0, 0, 0, 0], [255, 255, 255, 127], [1, 0, 0, 0, 255], Protocol.Encode(new(Kind.Motion, Protocol.Pack(new Motion { X = float.NaN }))) })
        {
            using var raw = new TcpClient(); await raw.ConnectAsync(server.Endpoint.Address, server.Endpoint.Port);
            await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello, Protocol.Hello(Versions.Current, Player("bad-frame"), ""))));
            await Read(raw);
            await raw.GetStream().WriteAsync(bad);
            await WaitCount(server, before);
        }
        Assert(survivor.IsConnected, "坏长度、未知类型、非法运动仅清理对应连接并释放名额");
        using (var slow = new TcpClient())
        { await slow.ConnectAsync(server.Endpoint.Address, server.Endpoint.Port); await slow.GetStream().WriteAsync(new byte[] { 2 }); await Task.Delay(2400); }
        var probe = await Connect(server, "after-bad");
        var got = false; survivor.MessageReceived += m => { if (m.Body.SequenceEqual(new byte[] { 123 })) got = true; };
        probe.SendToWorld(1, [123]); await Until(() => got); probe.Disconnect(); await WaitCount(server, before);
        Assert(got, "未完成帧超时后仍可接纳新玩家并通信");
    }

    static async Task LargeSnapshots()
    {
        await using var server = new Server(new() { MaxPlayers = 64 }); await server.StartAsync();
        var resources = new Resources { Ready = true, ExtraIds = [Enumerable.Range(0, 1900).ToArray(), [], [], [], [], [], [], [], []] };
        var joined = new List<Client>(); string failure = "";
        for (int i = 0; i < 40; i++)
        {
            var client = new Client(); clients.Add(client);
            failure = await Failure(client.ConnectAsync(server.Endpoint, Player("large" + i, resources)));
            if (failure != "UnexpectedSuccess") break;
            joined.Add(client);
        }
        Assert(failure == "WorldDataBudgetExceeded" && joined.Count > 2, "组合快照预算在入世界前拒绝，合法单包不能撑破快照");
        var host = joined[0]; await Pump(host.CreateRoomAsync(64)); await Pump(host.SetJoinableAsync(true));
        foreach (var c in joined.Skip(1)) await Pump(c.JoinRoomAsync(host.State.Room!.Id));
        Assert(host.State.Room!.Members.Length == joined.Count && Protocol.Pack(host.State).Length < Protocol.MaxFrame, "预算内所有大资源成员组成完整房间快照");
        foreach (var c in joined) c.Disconnect();
    }

    static async Task Lan()
    {
        await using var lan = new LanSession(maxPlayers: 2, messages: rules); clients.Add(lan.Client);
        await Pump(lan.StartAsync(Player("LAN-host")));
        Assert(lan.Client.State.Room != null && lan.Client.Uid > 0, "局域网同一客户端本机 TCP 自动建房");
        await Pump(lan.Client.SetJoinableAsync(true));
        var guest = new Client(); clients.Add(guest);
        await Pump(LanSession.JoinAsync(guest, new(IPAddress.Loopback, lan.Server.Endpoint.Port), Player("LAN-guest")));
        await Pump(lan.Client.SetRoomPlayerLimitAsync(3));
        Assert(lan.Client.State.MaxPlayers == 3 && lan.Client.State.Room!.MaxPlayers == 3, "局域网容量一次操作同步服务器与默认房间");
        guest.LeaveRoom(); await Until(() => !guest.IsConnected);
        Assert(!guest.IsConnected, "局域网退房结束 World 连接");
        var other = new Client(); clients.Add(other);
        await Pump(lan.Client.SetJoinableAsync(false));
        Assert(await Failure(LanSession.JoinAsync(other, new(IPAddress.Loopback, lan.Server.Endpoint.Port), Player("too-early"))) == "JoinClosed", "局域网自动入房失败明确返回并断开");
        await Pump(lan.Client.SetJoinableAsync(true));
        var final = new Client(); clients.Add(final);
        await Pump(LanSession.JoinAsync(final, new(IPAddress.Loopback, lan.Server.Endpoint.Port), Player("last")));
        lan.Client.Disconnect(); await Until(() => !final.IsConnected);
        Assert(!final.IsConnected, "局域网本地房主结束关闭服务器及其他连接");
    }
}

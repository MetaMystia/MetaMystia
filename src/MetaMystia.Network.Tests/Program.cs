using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

using MemoryPack;

using Common.UI;
using GameData.Core.Collections.CharacterUtility;

using MetaMystia;
using MetaMystia.Network;

try { await Checks.Run(); }
catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }

static partial class Checks
{
    static readonly List<Client> clients = [];
    static readonly MessageRule[] rules = [new(1, [Route.World], RoomScoped: false), new(2, [Route.Host]), new(3, [Route.Room, Route.Player], HostOnly: true), new(4, [Route.Room, Route.Player]), new(5, [Route.Server], RoomScoped: false)];
    static int checks;
    static Player Player(string name, Resources? resources = null) => new()
    {
        Name = name, Scene = Scene.DayScene, Skin = new() { SelectedType = CharacterSkinSets.SelectedType.Default },
        Resources = resources ?? new() { Ready = true, DlcFlags = DlcPack.Core, PackIds = ["test.pack"] },
        Motion = new() { X = 7, DirectionX = 1, Speed = 2, Map = MapLabel.Home }
    };
    static void Assert(bool value, string description)
    { if (!value) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
    static async Task Pump(Task task, int milliseconds = 10000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!task.IsCompleted && DateTime.UtcNow < end)
        { foreach (var c in clients.ToArray()) c.DispatchPending(); await Task.Delay(2); }
        if (!task.IsCompleted) throw new TimeoutException("Verification pump timeout");
        await task;
        foreach (var c in clients.ToArray()) c.DispatchPending();
    }
    static async Task Until(Func<bool> condition)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < end)
        { foreach (var c in clients.ToArray()) c.DispatchPending(); await Task.Delay(2); }
        if (!condition()) throw new Exception("Condition timeout");
    }
    static async Task<Client> Connect(Server server, string name, Versions? versions = null, Resources? resources = null)
    {
        var c = new Client(versions); clients.Add(c);
        await Pump(c.ConnectAsync(new(IPAddress.Loopback, server.Endpoint.Port), Player(name, resources)));
        return c;
    }
    static async Task<string> Failure(Task task)
    {
        try { await Pump(task); return "UnexpectedSuccess"; }
        catch (NetworkException e) { return e.Code; }
        catch (OperationCanceledException) { return "Cancelled"; }
        catch (TimeoutException) { return "Timeout"; }
    }
    static async Task<Frame> Read(TcpClient tcp)
    {
        using var deadline = new CancellationTokenSource(3000);
        var head = new byte[4]; await Connection.ReadExactly(tcp.GetStream(), head, deadline.Token);
        var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(head)];
        await Connection.ReadExactly(tcp.GetStream(), body, deadline.Token); return Protocol.Decode(body);
    }
    static async Task WaitCount(Server server, int count)
    {
        for (int n = 0; n < 500; n++)
        { if ((await server.GetSnapshotAsync()).World.Length == count) return; await Task.Delay(2); }
        throw new Exception("Server count timeout");
    }

    internal static async Task Run()
    {
        await StationaryHostSnapshot();
        var data = Player("真实枚举");
        Assert(MemoryPackSerializer.Deserialize<Player>(MemoryPackSerializer.Serialize(data))!.Scene == Scene.DayScene, "真实游戏枚举在无头进程读写");
        Console.WriteLine($"SIZE motion body={Protocol.Pack(data.Motion).Length}, frame={Protocol.Encode(new(Kind.Motion, Protocol.Pack(data.Motion))).Length}, resources={Protocol.Pack(data.Resources).Length}");
        await using var server = new Server(new() { MaxPlayers = 5, Messages = rules, Timeout = TimeSpan.FromSeconds(2) });
        await server.StartAsync();
        var a = await Connect(server, "A"); var b = await Connect(server, "B"); var c = await Connect(server, "C");
        await Until(() => a.State.World.Length == 3);
        b.SendMotion(new() { X = 7, Map = MapLabel.Home });
        await Until(() => a.State.World.Single(p => p.Uid == b.Uid).HasMotion);
        Assert(a.State.World.All(p => p.Resources == null) && a.State.World.Single(p => p.Uid == b.Uid).Motion.X == 7, "世界快照含最新资料与运动，资源表不外泄");
        await Pump(a.CreateRoomAsync(2)); var room1 = a.State.Room!.Id;
        Assert(!a.State.Room.Joinable, "建房默认关闭");
        Assert(await Failure(b.JoinRoomAsync(room1)) == "JoinClosed", "阶段关闭拒绝加入");
        await Pump(a.SetJoinableAsync(true));
        var joinB = b.JoinRoomAsync(room1); var joinC = c.JoinRoomAsync(room1);
        var combined = Task.WhenAll(Observe(joinB), Observe(joinC));
        await Pump(combined);
        var outcomes = await combined;
        Assert(outcomes.Count(s => s == "RoomFull") == 1 && outcomes.Count(s => s == "UnexpectedSuccess") == 1, "并发最后一个房间名额只成功一人");
        var guest = b.State.Room != null ? b : c;
        var outsider = guest == b ? c : b;
        Assert(guest.State.Room!.Members.All(p => p.Resources?.Ready == true) && guest.State.World.Length == 3, "入房资源快照完整且仍属于 World");
        await Pump(outsider.CreateRoomAsync(2)); await Pump(outsider.SetJoinableAsync(true));
        var receivedA = new List<ReceivedMessage>(); var receivedGuest = new List<ReceivedMessage>(); var receivedOutside = new List<ReceivedMessage>();
        a.MessageReceived += receivedA.Add; guest.MessageReceived += receivedGuest.Add; outsider.MessageReceived += receivedOutside.Add;
        for (int i = 0; i < 100; i++) a.SendToWorld(1, BitConverter.GetBytes(i));
        await Until(() => receivedGuest.Count == 100 && receivedOutside.Count == 100);
        Assert(receivedGuest.Select(m => BitConverter.ToInt32(m.Body)).SequenceEqual(Enumerable.Range(0, 100)) && receivedA.Count == 0, "发送、转发和显式派发保持顺序，世界广播跨房且不回送");
        a.SendToRoom(3, [42]); await Until(() => receivedGuest.Count == 101);
        await Task.Delay(30); outsider.DispatchPending();
        Assert(receivedOutside.Count == 100, "房间广播隔离");
        guest.SendToHost(2, [8], 77); await Until(() => receivedA.Count == 1);
        var context = receivedA[0].Context;
        a.Reply(context, 3, [9]); await Until(() => receivedGuest.Count == 102);
        Assert(receivedGuest[^1].Context.Sender == a.Uid && receivedGuest[^1].Context.Request == 77, "房主单独回复保留请求编号、来源是房主");
        a.SendToPlayer(outsider.Uid, 3, [99]); guest.SendToRoom(3, [99]);
        await Task.Delay(30); foreach (var x in clients) x.DispatchPending();
        Assert(receivedOutside.Count == 100 && receivedA.Count == 1, "拒绝跨房定向与非房主裁定");
        guest.LeaveRoom(); Assert(guest.State.Room == null, "主动退房立即清理本地上下文");
        await Until(() => a.State.Room!.Members.Length == 1);
        await Task.Delay(20); guest.DispatchPending();
        await Pump(guest.JoinRoomAsync(room1));
        a.Reply(context, 3, [66]); await Task.Delay(30); guest.DispatchPending();
        Assert(receivedGuest.Count == 102, "旧入房身份的迟到回复不污染再次入房");
        await Pump(a.SetRoomPlayerLimitAsync(1));
        Assert(a.State.Room!.Members.Length == 2 && a.State.Room.MaxPlayers == 1 && a.State.Room.Joinable, "下调容量保留成员且不修改开关");
        guest.LeaveRoom(); await Until(() => a.State.Room!.Members.Length == 1); await Task.Delay(20); guest.DispatchPending();
        Assert(await Failure(guest.JoinRoomAsync(room1)) == "RoomFull", "下调后限制新加入");
        await Pump(a.SetRoomPlayerLimitAsync(2));
        using (var cancel = new CancellationTokenSource(30))
        {
            var pending = guest.JoinRoomAsync(room1, cancel.Token);
            await Task.Delay(150); // 故意停派发，让成功包迟到到取消之后。
            Assert(await Failure(pending) == "Cancelled", "入房取消等待撤销确认");
        }
        Assert(guest.State.Room == null, "迟到成功不恢复本地房间");
        await Until(() => a.State.Room!.Members.Length == 1);
        Assert((await server.GetSnapshotAsync()).Rooms.Single(r => r.Id == room1).Count == 1, "撤销释放真实服务器成员");
        await Pump(guest.JoinRoomAsync(room1));
        a.StateChanged += () => throw new Exception("application callback");
        int ended = 0; a.ConnectionEnded += _ => { ended++; throw new Exception("application end"); };
        a.Disconnect(); a.DispatchPending(); a.DispatchPending();
        await Until(() => guest.State.Room == null && guest.State.World.Length == 2);
        Assert(ended == 1 && guest.IsConnected, "回调异常不妨碍一次性清理；无头房主离开解散房间并保留 World");
        foreach (var version in new[] { Versions.Current with { Protocol = 999 }, Versions.Current with { Game = "bad" }, Versions.Current with { Mod = "bad" } })
        {
            var wrong = new Client(version); clients.Add(wrong);
            string expected = version.Protocol != Versions.Current.Protocol ? "ProtocolMismatch" : version.Game == "bad" ? "GameMismatch" : "ModMismatch";
            Assert(await Failure(wrong.ConnectAsync(server.Endpoint, Player("bad"))) == expected, "稳定握手版本校验 " + expected);
        }
        await BadFrames(server, guest);
        var d = await Connect(server, "D"); var e = await Connect(server, "E"); var f = await Connect(server, "F");
        var full = new Client(); clients.Add(full);
        Assert(await Failure(full.ConnectAsync(server.Endpoint, Player("full"))) == "ServerFull", "World 上限包含房间玩家且不重复计数");
        await Pump(d.CreateRoomAsync(2)); await Pump(d.SetJoinableAsync(true)); await Pump(e.JoinRoomAsync(d.State.Room!.Id));
        Assert(e.State.Room != null, "World 满员不妨碍已有玩家入房");
        var oldUid = d.Uid;
        d.Disconnect(); await WaitCount(server, 4);
        await using (var second = new Server(new() { MaxPlayers = 1 }))
        {
            await second.StartAsync(); var secondClient = await Connect(second, "second");
            Assert(secondClient.Uid > oldUid && secondClient.Uid > f.Uid, "同一进程不同 Server 实例 UID 不复用");
            secondClient.Disconnect();
        }
        foreach (var x in clients) { x.Disconnect(); x.DispatchPending(); }
        await LargeSnapshots();
        await Lan();
        await Lifecycle();
        await Isolation();
        Console.WriteLine($"ALL PASS ({checks} assertions)");
    }

    static async Task<string> Observe(Task task)
    { try { await task; return "UnexpectedSuccess"; } catch (NetworkException e) { return e.Code; } }
}

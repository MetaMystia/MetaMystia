using System.Net.Sockets;

using MetaMystia.Network;

static partial class Checks
{
    static async Task Retirement()
    {
        await using var empty = new Server(new());
        await empty.StartAsync();
        Assert(await empty.DrainAsync("empty", "next") == NetworkErrorCode.None, "空服接受退役请求");
        await empty.Drained.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(empty.Drained.IsCompletedSuccessfully, "空服立即达到退出条件");

        await using var server = new Server(new() { Messages = rules });
        await server.StartAsync();
        var host = await Connect(server, "drainHost");
        var guest = await Connect(server, "drainGuest");
        await Pump(host.CreateRoomAsync(2));
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        var notices = new List<ServerNotice>();
        guest.NoticeReceived += notices.Add;
        await server.DrainAsync("release-a", "next");
        await Until(() => notices.Count == 1);
        await server.DrainAsync("release-a", "next");
        await Task.Delay(30); guest.DispatchPending();
        Assert(notices.Count == 1 && notices[0].TargetModVersion == "next", "结构化退役通知保留版本参数且重复请求不刷屏");
        Assert(host.IsConnected && guest.IsConnected && guest.State.Room != null && !server.Drained.IsCompleted, "退役保留已有连接和房间");
        var messages = 0;
        guest.MessageReceived += _ => messages++;
        host.SendToRoom(3, [42]);
        await Until(() => messages == 1);
        Assert(messages == 1, "退役后仍能转发房间玩法消息");
        var refused = new Client(); clients.Add(refused);
        Assert(await Failure(refused.ConnectAsync(server.Endpoint, Player("late"))) == "ServerRetiring", "退役拒绝新连接");
        host.Disconnect();
        await WaitCount(server, 1);
        Assert(!server.Drained.IsCompleted, "仍在大厅的玩家阻止退出");
        guest.Disconnect();
        await server.Drained.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(server.Drained.IsCompletedSuccessfully, "最后一名玩家离线触发退出条件");

        await using var protectedServer = new Server(new());
        await protectedServer.StartAsync();
        using var attacker = new TcpClient();
        await attacker.ConnectAsync(protectedServer.Endpoint);
        await attacker.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello, Protocol.Hello(Versions.Current, Player("attacker"), ""))));
        await Read(attacker);
        await attacker.GetStream().WriteAsync(Protocol.Encode(new(Kind.ServerNotice, Protocol.Pack(new ServerNotice
        { Code = ServerNoticeCode.Retiring, NoticeId = "forged", TargetModVersion = "bad" }))));
        await WaitCount(protectedServer, 0);
        Assert((await protectedServer.GetStatusAsync()).Retirement == null, "玩家不能伪造系统退役通知");
    }
}

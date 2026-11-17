using System.Net;
using System.Net.Sockets;

using MetaMystia.Network;

static partial class Checks
{
    static async Task LatencyChecks()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new Client();
        clients.Add(client);
        try
        {
            var connecting = client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, Player("latency"));
            using var peer = await listener.AcceptTcpClientAsync();
            peer.NoDelay = true;
            await Read(peer);
            await peer.GetStream().WriteAsync(Protocol.Encode(new(Kind.Welcome, Protocol.Pack(new Snapshot()), Sender: 1)));
            await Pump(connecting);
            Assert(client.Latency == 0 && client.State.Room == null, "世界连接首次测量前延迟为零");

            async Task<Frame> Probe()
            {
                await peer.GetStream().WriteAsync(Protocol.Encode(new(Kind.Ping, [])));
                Assert((await Read(peer)).Kind == Kind.Pong, "测量同时保留服务器心跳应答");
                var ping = await Read(peer);
                Assert(ping.Kind == Kind.Ping && ping.Body.Length == sizeof(long), "世界玩家自动发送服务器延迟探测");
                return ping;
            }

            var first = await Probe();
            await Task.Delay(120);
            await peer.GetStream().WriteAsync(Protocol.Encode(new(Kind.Pong, first.Body)));
            // 不派发主线程队列，确认延迟在网络接收时更新。
            for (int i = 0; i < 500 && client.Latency == 0; i++) await Task.Delay(2);
            Assert(client.Latency >= 50 && client.PendingFrames == 0, "延迟包含模拟往返等待，且不依赖主线程派发");
            long measured = client.Latency;
            var second = await Probe();
            await Task.Delay(220);
            await peer.GetStream().WriteAsync(Protocol.Encode(new(Kind.Pong, second.Body)));
            await Until(() => client.Latency != measured);
            Assert(client.Latency >= 100, "后续心跳持续更新延迟");
            client.Disconnect();
            client.DispatchPending();
            Assert(client.Latency == 0, "断开立即清除服务器延迟");
        }
        finally { listener.Stop(); }

        await using var server = new Server(new() { Timeout = TimeSpan.FromMilliseconds(600) });
        await server.StartAsync();
        await Pump(client.ConnectAsync(server.Endpoint, Player("reconnected")));
        Assert(client.Latency == 0, "重连不继承旧连接延迟");
        await Pump(client.CreateRoomAsync());
        var guest = await Connect(server, "latency-guest");
        await Pump(client.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(client.State.Room!.Id));
        await Task.Delay(800);
        await Until(() => client.IsConnected && guest.IsConnected);
        Assert(client.State.Room!.Host == client.Uid && guest.State.Room != null, "房主和客机持续心跳不会断线");
        guest.LeaveRoom();
        await Until(() => client.State.Room!.Members.Length == 1);
        await Task.Delay(800);
        guest.DispatchPending();
        Assert(guest.IsConnected && guest.State.Room == null, "退房后世界连接继续心跳");

        using var raw = new TcpClient();
        await raw.ConnectAsync(server.Endpoint);
        await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello, Protocol.Hello(Versions.Current, Player("raw-latency"), ""))));
        Assert((await Read(raw)).Kind == Kind.Welcome, "延迟测试原始连接握手成功");
        byte[] stamp = BitConverter.GetBytes(123456L);
        await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Ping, stamp)));
        Frame reply;
        do { reply = await Read(raw); } while (reply.Kind != Kind.Pong);
        Assert(reply.Body.SequenceEqual(stamp), "真实服务器原样回送 Ping 时间戳");
        client.Disconnect();
        guest.Disconnect();
    }
}

using System.Net;
using System.Net.Sockets;

using Common.UI;

using MetaMystia.Network;

static partial class Checks
{
    static async Task RoomExits()
    {
        await using var server = new Server(new() { MaxPlayers = 3 });
        await server.StartAsync();
        var host = await Connect(server, "exit-host");
        var guest = await Connect(server, "exit-guest");
        await Pump(host.CreateRoomAsync(2));
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        guest.SetProfile("exit-guest", Player("exit-guest").Skin, Scene.WorkScene, GameStage.Work);
        guest.LeaveRoom();
        Assert(guest.IsConnected && guest.State.Room == null && !guest.State.IsLan,
            "独立服务器夜间客人退房保留世界连接");
        await Until(() => host.State.Room!.Members.Length == 1);
        guest.SetProfile("exit-guest", Player("exit-guest").Skin, Scene.DayScene, GameStage.Day);
        await Until(() => host.State.World.Single(p => p.Uid == guest.Uid).Stage == GameStage.Day);
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        host.LeaveRoom();
        await Until(() => guest.State.Room == null);
        Assert(host.IsConnected && guest.IsConnected && guest.State.World.Length == 2,
            "独立服务器房主退房解散房间但双方保留连接，客人可重新入房");
        host.Disconnect();
        guest.Disconnect();

        await using var lan = new LanSession(maxPlayers: 3);
        clients.Add(lan.Client);
        await Pump(lan.StartAsync(Player("raw-exit-host")));
        await Pump(lan.Client.SetJoinableAsync(true));
        using var raw = new TcpClient();
        await raw.ConnectAsync(IPAddress.Loopback, lan.Server.Endpoint.Port);
        await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello,
            Protocol.Hello(Versions.Current, Player("raw-exit-guest"), ""))));
        var welcome = await Read(raw);
        var state = Protocol.Read<Snapshot>(welcome.Body);
        await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Command, Protocol.Pack(new Control
        {
            Command = Command.Leave, Request = 1, Room = state.Room!.Id,
            Membership = state.Room.Members.Single(p => p.Uid == welcome.Sender).Membership
        }))));
        bool worldOnly = false;
        try
        {
            while (true)
            {
                var frame = await Read(raw);
                if (frame.Kind == Kind.Snapshot) worldOnly |= Protocol.Read<Snapshot>(frame.Body).Room == null;
            }
        }
        catch (EndOfStreamException) { }
        Assert(!worldOnly, "服务端收到 LAN 退房命令直接关闭连接，不下发无房间快照");
        await WaitCount(lan.Server, 1);
        var kicked = await Connect(lan.Server, "kicked");
        kicked.StateChanged += () => worldOnly |= kicked.IsConnected && kicked.State.Room == null;
        await Pump(lan.Client.KickAsync(kicked.Uid));
        await Until(() => !kicked.IsConnected);
        Assert(!worldOnly, "LAN 踢人直接断线，不残留世界玩家状态");
    }
}

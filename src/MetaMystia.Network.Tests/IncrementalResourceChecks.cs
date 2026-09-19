using System.Net.Sockets;

using MetaMystia.Network;

static partial class Checks
{
    static async Task IncrementalResources()
    {
        await using var server = new Server(new() { Timeout = TimeSpan.FromSeconds(30) });
        await server.StartAsync();
        var host = await Connect(server, "resource-host");
        await Pump(host.CreateRoomAsync(4));
        await Pump(host.SetJoinableAsync(true));
        using var viewer = new TcpClient();
        await viewer.ConnectAsync(server.Endpoint.Address, server.Endpoint.Port);
        await viewer.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello,
            Protocol.Hello(Versions.Current, Player("resource-viewer"), ""))));
        await Read(viewer);
        await viewer.GetStream().WriteAsync(Protocol.Encode(new(Kind.Command,
            Protocol.Pack(new Control { Command = Command.Join, Request = 1, Room = host.State.Room!.Id }))));
        var initial = Protocol.Read<Snapshot>((await Read(viewer)).Body);
        await Read(viewer); // 入房确认
        Assert(initial.Room!.Members.All(p => p.Resources != null), "新成员首次收到完整房间资源");

        var guest = await Connect(server, "resource-new");
        var worldUpdate = Protocol.Read<Snapshot>((await Read(viewer)).Body);
        Assert(worldUpdate.Room!.Members.All(p => p.Resources == null), "世界成员变化不重发已有房间资源");
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        var joined = Protocol.Read<Snapshot>((await Read(viewer)).Body);
        Assert(joined.Room!.Members.Count(p => p.Resources != null) == 1
            && joined.Room.Members.Single(p => p.Resources != null).Uid == guest.Uid,
            "新玩家入房时老成员只收到新玩家的资源");
        Assert(host.State.Room!.Members.All(p => p.Resources != null)
            && guest.State.Room!.Members.All(p => p.Resources != null), "客户端对外仍提供完整资源快照");

        var originalMembership = guest.State.Room!.Members.Single(p => p.Uid == guest.Uid).Membership;
        guest.LeaveRoom();
        await Until(() => host.State.Room!.Members.Length == 2);
        var left = Protocol.Read<Snapshot>((await Read(viewer)).Body);
        Assert(left.Room!.Members.All(p => p.Resources == null), "成员离房不重发剩余成员资源");
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        var rejoined = Protocol.Read<Snapshot>((await Read(viewer)).Body);
        var refreshed = rejoined.Room!.Members.Single(p => p.Uid == guest.Uid);
        Assert(refreshed.Membership != originalMembership && refreshed.Resources != null
            && guest.State.Room!.Members.All(p => p.Resources != null), "再次入房重新初始化资源，不沿用旧入房缓存");
        host.Disconnect();
        guest.Disconnect();
    }
}

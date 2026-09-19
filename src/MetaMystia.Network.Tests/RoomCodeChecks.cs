using System.Net.Sockets;

using MetaMystia.Network;

static partial class Checks
{
    static async Task RoomCodes()
    {
        Assert(RoomCode.TryParse("AB20", out var upper) && RoomCode.TryParse("ab20", out var lower)
            && upper == lower && RoomCode.Format(lower) == "AB20", "房间码输入大小写不敏感，显示统一大写");
        Assert(RoomCode.Format(1) == "0001" && RoomCode.TryParse("FFFF", out var last) && last == ushort.MaxValue,
            "房间码补足四位并支持完整无符号 16 位范围");
        Assert(new[] { "", "0", "0000", "123", "12345", "0xAB", "ZZZZ", " AB2" }.All(s => !RoomCode.TryParse(s, out _)),
            "拒绝保留编号、非四位或非十六进制输入");
        foreach (var frame in new[]
        {
            new Frame(Kind.RoomMotion, [1], Room: ushort.MaxValue, Membership: 70000, RecipientMembership: 80000),
            new Frame(Kind.Data, [2], Type: 1, Route: Route.Room, Room: ushort.MaxValue, Membership: 70000, RecipientMembership: 80000)
        })
        {
            var decoded = Protocol.Decode(Protocol.Encode(frame)[4..]);
            Assert(decoded.Room == frame.Room && decoded.Membership == frame.Membership
                && decoded.RecipientMembership == frame.RecipientMembership && decoded.Body.SequenceEqual(frame.Body),
                $"{frame.Kind} 的 16 位房间码与 64 位成员代号正确往返");
        }
        await using var server = new Server(new() { MaxPlayers = 24 });
        await server.StartAsync();
        var owners = new List<Client>();
        for (int i = 0; i < 16; i++)
        {
            var owner = await Connect(server, "room-owner-" + i);
            owners.Add(owner);
            await Pump(owner.CreateRoomAsync());
            await Pump(owner.SetJoinableAsync(true));
        }
        var ids = owners.Select(c => c.State.Room!.Id).ToArray();
        Assert(ids.All(id => id != 0) && ids.Distinct().Count() == ids.Length,
            "服务器分配非零且互不重复的房间码并回传创建者");
        var guest = await Connect(server, "code-guest");
        RoomCode.TryParse(RoomCode.Format(ids[0]).ToLowerInvariant(), out var target);
        await Pump(guest.JoinRoomAsync(target));
        Assert(guest.State.Room?.Id == ids[0], "小写房间码解析后可加入服务器分配的房间");
        using var raw = new TcpClient();
        await raw.ConnectAsync(server.Endpoint.Address, server.Endpoint.Port);
        await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello, Protocol.Hello(Versions.Current, Player("raw-creator"), ""))));
        var welcome = await Read(raw);
        await raw.GetStream().WriteAsync(Protocol.Encode(new(Kind.Command,
            Protocol.Pack(new Control { Command = Command.Create, Request = 1, Value = 2, Room = ids[0] }))));
        Frame reply;
        do { reply = await Read(raw); } while (reply.Kind != Kind.Ack);
        var created = (await server.GetSnapshotAsync()).Rooms.Single(r => r.Host == welcome.Sender);
        Assert(created.Id != ids[0] && created.Id != 0 && Protocol.Read<Control>(reply.Body).Error.Code == NetworkErrorCode.None,
            "创建请求携带已有房间码也不能指定或覆盖服务器编号");
        guest.Disconnect();
        foreach (var owner in owners) owner.Disconnect();
    }
}

using MetaMystia.Network;

static partial class Checks
{
    static async Task<NetworkError> ErrorFrom(Task operation)
    {
        try { await Pump(operation); }
        catch (NetworkException error) { return error.Error; }
        throw new Exception("Expected network error");
    }

    static async Task ErrorDetails()
    {
        var versions = Versions.Current;
        await using var server = new Server(new() { MaxPlayers = 2, Versions = versions });
        await server.StartAsync();
        foreach (var (version, code) in new[]
        {
            (versions with { Protocol = versions.Protocol + 1 }, NetworkErrorCode.ProtocolMismatch),
            (versions with { Game = "different" }, NetworkErrorCode.GameMismatch),
            (versions with { Mod = "different" }, NetworkErrorCode.ModMismatch)
        })
        {
            var client = new Client(version);
            clients.Add(client);
            var error = await ErrorFrom(client.ConnectAsync(server.Endpoint, Player("Mismatch")));
            Assert(error.Code == code && error.ProtocolVersion == versions.Protocol
                && error.GameVersion == versions.Game && error.ModVersion == versions.Mod,
                $"{code} 拒绝保留对端完整版本");
            await WaitCount(server, 0);
        }
        var host = await Connect(server, "ErrorHost");
        var guest = await Connect(server, "ErrorGuest");
        Assert(host.State.Room == null && guest.State.Room == null,
            "同一普通连接入口连接独立服务器时只进入世界");
        var overflow = new Client();
        clients.Add(overflow);
        var full = await ErrorFrom(overflow.ConnectAsync(server.Endpoint, Player("Overflow")));
        Assert(full.Code == NetworkErrorCode.ServerFull && full.Count == 2 && full.Limit == 2,
            "服务器满员拒绝携带人数和上限");
        await Pump(host.CreateRoomAsync(1));
        await Pump(host.SetJoinableAsync(true));
        var roomFull = await ErrorFrom(guest.JoinRoomAsync(host.State.Room!.Id));
        Assert(roomFull.Code == NetworkErrorCode.RoomFull && roomFull.Count == 1 && roomFull.Limit == 1,
            "入房失败回复携带人数和上限");
        var unknown = Protocol.ReadError(Protocol.WriteError(new() { Code = (NetworkErrorCode)65000 }));
        Assert((ushort)unknown.Code == 65000, "未知数字错误码可读取并交给界面回退");
        host.Disconnect(); guest.Disconnect();
    }
}

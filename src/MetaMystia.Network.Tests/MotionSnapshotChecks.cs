using System.Net;

using MetaMystia;
using MetaMystia.Network;

static partial class Checks
{
    static async Task StationaryHostSnapshot()
    {
        await using var lan = new LanSession(maxPlayers: 2);
        clients.Add(lan.Client);
        await Pump(lan.StartAsync(Player("stationary-host")));
        var motion = new Motion { X = 17, Y = 23, Speed = 2, Map = MapLabel.Home };
        lan.Client.SendMotion(motion);
        await Pump(lan.Client.SetJoinableAsync(true));
        var stored = (await lan.Server.GetSnapshotAsync()).World.Single(p => p.Uid == lan.Client.Uid);
        Assert(stored.HasMotion && stored.Motion == motion, "服务端保存静止房主的最新位置");

        var guest = new Client(); clients.Add(guest);
        await Pump(guest.ConnectAsync(new IPEndPoint(IPAddress.Loopback, lan.Server.Endpoint.Port), Player("new-guest")));
        var host = guest.State.Room!.Members.Single(p => p.Uid == lan.Client.Uid);
        Assert(host.HasMotion && host.Motion == motion, "房主不再发包，新客机仍从入房快照得到位置");

        int updates = 0;
        guest.StateChanged += () => updates++;
        lan.Client.SendMotion(motion);
        await Until(() => updates > 0);
        Assert(guest.State.Room!.Members.Single(p => p.Uid == lan.Client.Uid).Motion == motion,
            "重复的静止位置仍向客户端派发状态更新");
    }
}

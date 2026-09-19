using System.Net;

namespace MetaMystia.Network;

// 在游戏线程持续调用 Client.DispatchPending；创建成功后由应用层打开入房。
public sealed class LanSession : IAsyncDisposable
{
    public Client Client { get; }
    public Server Server { get; }
    private readonly string key = Guid.NewGuid().ToString("N");
    public LanSession(int port = 0, int maxPlayers = 2, MessageRule[]? messages = null, Versions? versions = null, bool ipv6 = false)
    {
        Client = new(versions);
        Server = new(new ServerOptions { Address = ipv6 ? IPAddress.IPv6Any : IPAddress.Any, Port = port, MaxPlayers = maxPlayers, Messages = messages ?? Messages.DefaultRules(), Versions = versions ?? Versions.Current, LanKey = key });
    }
    public async Task StartAsync(Player player, CancellationToken token = default)
    {
        try
        {
            await Server.StartAsync().ConfigureAwait(false);
            await Client.ConnectCore(new(IPAddress.Loopback, Server.Endpoint.Port), player, key, token).ConfigureAwait(false);
        }
        catch { Client.Disconnect(); await Server.StopAsync().ConfigureAwait(false); throw; }
    }
    public async ValueTask DisposeAsync()
    { Client.Disconnect(); await Server.StopAsync().ConfigureAwait(false); }
}

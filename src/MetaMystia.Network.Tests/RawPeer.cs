using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

using MetaMystia.Network;

// 测试用裸连接：直接驱动传输层，绕过 Client 的握手和状态机。
sealed class RawPeer : IDisposable
{
    private readonly Channel<Frame> incoming = Channel.CreateUnbounded<Frame>();
    private readonly Connection wire;

    private RawPeer(TcpClient tcp, TimeSpan timeout)
    {
        wire = new(tcp, server: false, timeout, frame => incoming.Writer.TryWrite(frame), _ => incoming.Writer.TryComplete());
        wire.Start();
    }

    internal static async Task<RawPeer> Connect(Server server)
    {
        var tcp = new TcpClient(server.Endpoint.AddressFamily);
        var address = server.Endpoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
        await tcp.ConnectAsync(address, server.Endpoint.Port);
        return new(tcp, TimeSpan.FromSeconds(3));
    }

    internal void Send(Frame frame) => wire.Send(frame);
    internal void SendBytes(byte[] bytes) => wire.SendBytes(bytes);

    internal async Task<Frame> Read()
    {
        using var deadline = new CancellationTokenSource(3000);
        try { return await incoming.Reader.ReadAsync(deadline.Token); }
        catch (ChannelClosedException) { throw new EndOfStreamException(); }
    }

    public void Dispose() => wire.Close(NetworkErrorCode.Disconnected);
}

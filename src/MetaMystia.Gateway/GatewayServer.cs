using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using MetaMystia.Network;

namespace MetaMystia.Gateway;

public sealed class GatewayServer(IPEndPoint endpoint, RouteCache routes, int maxConnections = 256) : IAsyncDisposable
{
    private readonly TcpListener listener = new(endpoint);
    private readonly CancellationTokenSource stopped = new();
    private readonly ConcurrentDictionary<int, Task> connections = new();
    private Task accepting = Task.CompletedTask;
    private int sequence;
    public IPEndPoint Endpoint => (IPEndPoint)listener.LocalEndpoint;
    public int ConnectionCount => connections.Count;
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public event Action<string>? Logged;

    public void Start()
    {
        if (maxConnections < 1) throw new ArgumentOutOfRangeException(nameof(maxConnections));
        if (endpoint.Address.Equals(IPAddress.IPv6Any)) listener.Server.DualMode = true;
        listener.Start();
        accepting = Accept();
    }

    private async Task Accept()
    {
        try
        {
            while (!stopped.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stopped.Token);
                if (connections.Count >= maxConnections) { client.Dispose(); continue; }
                int id = Interlocked.Increment(ref sequence);
                var task = Forward(client);
                connections[id] = task;
                _ = task.ContinueWith(completed => { connections.TryRemove(id, out _); }, TaskScheduler.Default);
            }
        }
        catch (Exception error) when (stopped.IsCancellationRequested && error is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    private async Task Forward(TcpClient client)
    {
        using (client)
        using (var backend = new TcpClient())
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token))
        {
            client.NoDelay = backend.NoDelay = true;
            var stream = client.GetStream();
            bool forwarding = false;
            bool helloRead = false;
            try
            {
                deadline.CancelAfter(HandshakeTimeout);
                var header = new byte[4];
                await Connection.ReadExactly(stream, header, deadline.Token);
                int length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length < 1 || length > Protocol.MaxFrame) throw new InvalidDataException("Invalid frame length");
                var frame = new byte[length];
                await Connection.ReadExactly(stream, frame, deadline.Token);
                helloRead = true;
                if (frame[0] != (byte)Kind.Hello) throw new InvalidDataException("Expected Hello");
                var versions = Protocol.ReadHelloVersions(frame[1..]);
                var route = routes.Resolve(versions);
                await backend.ConnectAsync(route.Host!, route.Port!.Value, deadline.Token);
                var destination = backend.GetStream();
                await destination.WriteAsync(header, deadline.Token);
                await destination.WriteAsync(frame, deadline.Token);
                forwarding = true;
                deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                Logged?.Invoke($"连接已转发至实例 {route.InstanceId}。");
                var upstream = Copy(stream, destination, deadline.Token);
                var downstream = Copy(destination, stream, deadline.Token);
                await Task.WhenAny(upstream, downstream);
                deadline.Cancel();
                await Task.WhenAll(upstream, downstream);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SocketException or OperationCanceledException or ObjectDisposedException or NetworkException or FormatException or ArgumentException)
            {
                if (!forwarding && helloRead && !stopped.IsCancellationRequested)
                {
                    NetworkError rejection = error is NetworkException network ? network.Error :
                        error is InvalidDataException or EndOfStreamException or FormatException or ArgumentException
                            ? NetworkErrorCode.InvalidHello : NetworkErrorCode.ConnectFailed;
                    await Reject(stream, rejection);
                }
            }
        }
    }

    private async Task Reject(NetworkStream stream, NetworkError error)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(1));
        try { await stream.WriteAsync(Protocol.Encode(new(Kind.Rejected, Protocol.WriteError(error))), deadline.Token); }
        catch (Exception failure) when (failure is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    private async Task Copy(NetworkStream source, NetworkStream destination, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        while (!token.IsCancellationRequested)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(IdleTimeout);
            int count = await source.ReadAsync(buffer, deadline.Token);
            if (count == 0) return;
            await destination.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        stopped.Cancel(); listener.Stop();
        await accepting;
        await Task.WhenAll(connections.Values);
        stopped.Dispose();
    }
}

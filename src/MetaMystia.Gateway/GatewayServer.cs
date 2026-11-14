using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using MetaMystia.Network;

namespace MetaMystia.Gateway;

public sealed class GatewayServer(IPEndPoint endpoint, InstanceRegistry routes, int maxConnections = 256, GatewayStats? stats = null) : IAsyncDisposable
{
    private readonly GatewayStats stats = stats ?? new();
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

    private void Log(string level, string message)
    {
        stats.Logs.Add(level, message);
        Logged?.Invoke(message);
    }

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
                if (connections.Count >= maxConnections) { stats.Overflowed(); client.Dispose(); continue; }
                int id = Interlocked.Increment(ref sequence);
                stats.Opened();
                var task = Forward(client);
                connections[id] = task;
                _ = task.ContinueWith(completed => { connections.TryRemove(id, out _); stats.Closed(); }, TaskScheduler.Default);
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
            ConnectionRecord? record = null;
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
                record = stats.Begin((IPEndPoint)client.Client.RemoteEndPoint!, versions);
                var route = routes.Resolve(versions);
                record.InstanceId = route.Id;
                await backend.ConnectAsync(route.Registration.Host, route.Registration.Port, deadline.Token);
                var destination = backend.GetStream();
                await destination.WriteAsync(header, deadline.Token);
                await destination.WriteAsync(frame, deadline.Token);
                forwarding = true;
                stats.Forwarding(record, route.Id);
                Interlocked.Add(ref record.bytesIn, header.Length + frame.Length);
                deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                Log("info", $"连接已转发至实例 {route.Id}。");
                var upstream = Copy(stream, destination, record, true, deadline.Token);
                var downstream = Copy(destination, stream, record, false, deadline.Token);
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
                    if (record == null) stats.Invalid();
                    else
                    {
                        stats.Rejected(record, rejection.Code);
                        Log("warning", $"拒绝了模组 {ServerLogEntry.Quote(record.ModVersion)} 的连接：{rejection.Code}。");
                    }
                    await Reject(stream, rejection);
                }
                else if (!helloRead && !stopped.IsCancellationRequested) stats.Invalid();
            }
            finally { if (record != null) stats.Ended(record); }
        }
    }

    private async Task Reject(NetworkStream stream, NetworkError error)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(1));
        try { await stream.WriteAsync(Protocol.Encode(new(Kind.Rejected, Protocol.WriteError(error))), deadline.Token); }
        catch (Exception failure) when (failure is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    private async Task Copy(NetworkStream source, NetworkStream destination, ConnectionRecord record, bool inbound, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        while (!token.IsCancellationRequested)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(IdleTimeout);
            int count = await source.ReadAsync(buffer, deadline.Token);
            if (count == 0) return;
            await destination.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
            if (inbound) Interlocked.Add(ref record.bytesIn, count); else Interlocked.Add(ref record.bytesOut, count);
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

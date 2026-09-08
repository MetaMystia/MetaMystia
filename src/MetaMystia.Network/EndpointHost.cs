using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using MetaMystia.Protocol;

namespace MetaMystia.Network.Core;

// 直连宿主与独立服务器使用同一个装配；进程内玩家同样经过 Endpoint.Receive。
public sealed class EndpointHost : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<TcpClient> _accepted = new();
    private readonly Dictionary<long, TcpConnection> _connections = new();
    private readonly Queue<Delivery> _local = new();
    private long _nextConnection = 1;
    public Endpoint Endpoint { get; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public string? ListenerError { get; private set; }

    public EndpointHost(int port, bool ipv6 = false, int maxPlayers = 64, IPAddress? bindAddress = null)
    {
        Endpoint = new(maxPlayers);
        _listener = new(bindAddress ?? (ipv6 ? IPAddress.IPv6Any : IPAddress.Any), port);
        if (ipv6) _listener.Server.DualMode = true;
        _listener.Start();
        _ = AcceptLoop();
    }

    public void AttachLocal(long now) => Endpoint.Accept(0, now);
    public void ReceiveLocal(Message message, long now) => Endpoint.Receive(0, message, now);
    public bool TryDequeueLocal(out Delivery delivery) => _local.TryDequeue(out delivery!);

    public void Pump(long now)
    {
        while (_accepted.TryDequeue(out var client))
        {
            long id = _nextConnection++;
            _connections.Add(id, new(client));
            Endpoint.Accept(id, now);
        }
        foreach (var pair in _connections.ToArray())
        {
            while (pair.Value.TryDequeue(out var received))
            {
                if (received.Message != null) Endpoint.Receive(pair.Key, received.Message, now);
                else { Endpoint.Disconnect(pair.Key); _connections.Remove(pair.Key); break; }
            }
        }
        Endpoint.Tick(now);
        while (Endpoint.TryDequeue(out var delivery))
        {
            if (delivery.ConnectionId == 0) _local.Enqueue(delivery);
            else if (_connections.TryGetValue(delivery.ConnectionId, out var connection)) connection.Send(delivery.Message, delivery.Close);
        }
    }

    private async Task AcceptLoop()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                if (_accepted.Count >= Endpoint.MaxPlayers * 2 || _stop.IsCancellationRequested) client.Dispose();
                else _accepted.Enqueue(client);
            }
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException or ObjectDisposedException)
        { if (!_stop.IsCancellationRequested) ListenerError = error.Message; }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        foreach (var connection in _connections.Values) connection.Dispose();
        _connections.Clear();
        while (_accepted.TryDequeue(out var client)) client.Dispose();
    }
}

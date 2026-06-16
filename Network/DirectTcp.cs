using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MetaMystia.Protocol.Transport;

namespace MetaMystia.Network;

/// <summary>直连 TCP；仅在 MpWire IO 线程调用 <see cref="Pump"/>。</summary>
internal sealed class DirectTcp(Action<int, NetPacket> onPacket, Action<int> onClientLeft)
{
    private sealed class Link
    {
        public TcpClient Tcp;
        public NetworkStream Stream;
        public readonly PacketBuffer Buffer = new();
        public readonly Queue<byte[]> Pending = new();
    }

    private TcpListener _listener;
    private readonly Dictionary<int, Link> _clients = new();
    private Link _uplink;
    private int _nextUid;
    private bool _isHost;

    public bool IsHost => _isHost;
    public bool HasClients => _clients.Count > 0;
    public bool IsClientConnected => _uplink != null;

    public void StartHost(int port, bool ipv6)
    {
        Stop();
        _isHost = true;
        if (ipv6)
        {
            _listener = new TcpListener(IPAddress.IPv6Any, port)
            {
                Server =
                {
                    DualMode = true
                }
            };
        }
        else
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }
        _listener.Start();
    }

    public void ConnectClient(string host, int port, int timeoutMs)
    {
        Stop();
        _isHost = false;
        var tcp = IPAddress.TryParse(host, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6
            ? new TcpClient(AddressFamily.InterNetworkV6)
            : new TcpClient();
        tcp.ReceiveTimeout = timeoutMs;
        tcp.SendTimeout = 10_000;

        var connectTask = tcp.ConnectAsync(host ?? string.Empty, port);
        if (!connectTask.Wait(timeoutMs))
        {
            tcp.Dispose();
            throw new TimeoutException($"Connect to {host}:{port} timed out");
        }
        _uplink = new Link { Tcp = tcp, Stream = tcp.GetStream() };
    }

    public void Enqueue(int? targetUid, int? exceptUid, byte[] framed, bool dropIfCongested)
    {
        if (_isHost)
        {
            if (targetUid is { } uid && _clients.TryGetValue(uid, out var one))
            {
                TryEnqueue(one, framed, dropIfCongested);
                return;
            }
            foreach (var kvp in _clients)
            {
                if (exceptUid is { } ex && kvp.Key == ex) continue;
                TryEnqueue(kvp.Value, framed, dropIfCongested);
            }
        }
        else if (_uplink != null)
        {
            TryEnqueue(_uplink, framed, dropIfCongested);
        }
    }

    public void DisconnectClient(int uid)
    {
        if (_clients.TryGetValue(uid, out var link))
            TeardownLink(uid, link);
    }

    public void DisconnectAll()
    {
        foreach (var uid in new List<int>(_clients.Keys))
            DisconnectClient(uid);
    }

    public void Stop()
    {
        if (_uplink != null)
        {
            CloseLink(_uplink);
            _uplink = null;
        }
        DisconnectAll();
        try { _listener?.Stop(); }
        catch
        {
            // ignored
        }

        _listener = null;
        _isHost = false;
    }

    public void Pump()
    {
        if (_isHost)
        {
            AcceptPending();
            foreach (var kvp in new List<KeyValuePair<int, Link>>(_clients))
                PumpLink(kvp.Key, kvp.Value);
        }
        else if (_uplink != null)
        {
            PumpLink(MpConstants.HostUid, _uplink);
        }
    }

    private void AcceptPending()
    {
        if (_listener == null) return;
        while (_listener.Pending())
        {
            var tcp = _listener.AcceptTcpClient();
            tcp.NoDelay = true;
            int uid = System.Threading.Interlocked.Increment(ref _nextUid);
            _clients[uid] = new Link { Tcp = tcp, Stream = tcp.GetStream() };
        }
    }

    private void PumpLink(int uid, Link link)
    {
        if (link.Stream == null) return;

        try
        {
            if (IsSocketDisconnected(link))
                throw new IOException("Remote closed");

            FlushPending(link);

            var stream = link.Stream;
            if (stream == null || !stream.DataAvailable) return;

            byte[] recv = new byte[4096];
            while (stream.DataAvailable)
            {
                int read = stream.Read(recv, 0, recv.Length);
                if (read == 0) throw new IOException("Remote closed");
                link.Buffer.Write(recv, 0, read);
                foreach (var packet in link.Buffer.ExtractPackets())
                    onPacket(uid, packet);
            }
        }
        catch (Exception)
        {
            TeardownLink(uid, link);
        }
    }

    private void TeardownLink(int uid, Link link)
    {
        if (_isHost)
        {
            if (!_clients.Remove(uid)) return;
            CloseLink(link);
            onClientLeft(uid);
        }
        else if (_uplink == link)
        {
            CloseLink(link);
            _uplink = null;
            onClientLeft(MpConstants.HostUid);
        }
    }

    private static bool IsSocketDisconnected(Link link)
    {
        try
        {
            var socket = link.Tcp?.Client;
            if (socket == null) return true;
            return socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch
        {
            return true;
        }
    }

    private static void FlushPending(Link link)
    {
        if (link.Stream == null) return;
        while (link.Pending.Count > 0)
        {
            var data = link.Pending.Dequeue();
            link.Stream.Write(data, 0, data.Length);
        }
    }

    private static void TryEnqueue(Link link, byte[] framed, bool dropIfCongested)
    {
        if (dropIfCongested && link.Pending.Count > 32) return;
        if (link.Pending.Count > 512) return;
        link.Pending.Enqueue(framed);
    }

    private static void CloseLink(Link link)
    {
        try { link.Stream?.Dispose(); }
        catch {
            // ignored
        }

        try { link.Tcp?.Close(); }
        catch
        {
            // ignored
        }

        link.Stream = null;
        link.Pending.Clear();
    }
}

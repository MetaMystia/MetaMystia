using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MemoryPack;

namespace MetaMystia.Network;

/// <summary>线层信封：每帧恰好承载一个 <see cref="Action"/>。</summary>
[MemoryPackable]
public partial class NetPacket
{
    public Action Action { get; set; }

    public byte[] ToBytesWithLength()
    {
        byte[] body = MemoryPackSerializer.Serialize(this);
        byte[] result = new byte[4 + body.Length];
        BitConverter.GetBytes(body.Length).CopyTo(result, 0);
        Buffer.BlockCopy(body, 0, result, 4, body.Length);
        return result;
    }

    public static NetPacket FromBytes(byte[] data) =>
        MemoryPackSerializer.Deserialize<NetPacket>(data)!;

    public static NetPacket FromAction(Action action) => new(action);

    public NetPacket(Action action) => Action = action;
}

public sealed class PacketBuffer
{
    private MemoryStream buffer = new();

    public void Write(byte[] data, int offset, int count)
    {
        buffer.Position = buffer.Length;
        buffer.Write(data, offset, count);
        buffer.Position = 0;
    }

    public List<NetPacket> ExtractPackets()
    {
        var packets = new List<NetPacket>();
        while (true)
        {
            if (buffer.Length - buffer.Position < 4) break;
            byte[] lenBytes = new byte[4];
            buffer.Read(lenBytes, 0, 4);
            int bodyLength = BitConverter.ToInt32(lenBytes, 0);
            if (buffer.Length - buffer.Position < bodyLength)
            {
                buffer.Position -= 4;
                break;
            }
            byte[] body = new byte[bodyLength];
            buffer.Read(body, 0, bodyLength);
            packets.Add(NetPacket.FromBytes(body));
        }

        if (buffer.Position < buffer.Length)
        {
            byte[] leftover = buffer.ToArray()[(int)buffer.Position..];
            buffer = new MemoryStream();
            buffer.Write(leftover, 0, leftover.Length);
            buffer.Position = 0;
        }
        else
        {
            buffer = new MemoryStream();
        }

        return packets;
    }
}

/// <summary>直连 TCP；仅在 MpWire IO 线程调用 <see cref="Pump"/>。</summary>
internal sealed class DirectTcp
{
    private sealed class Link
    {
        public TcpClient Tcp;
        public NetworkStream Stream;
        public PacketBuffer Buffer = new();
        public readonly Queue<byte[]> Pending = new();
    }

    private readonly Action<int, NetPacket> _onPacket;
    private readonly Action<int> _onClientLeft;

    private TcpListener _listener;
    private readonly Dictionary<int, Link> _clients = new();
    private Link _uplink;
    private int _uplinkUid = MpConstants.UnassignedUid;
    private int _nextUid;
    private bool _isHost;

    public DirectTcp(Action<int, NetPacket> onPacket, Action<int> onClientLeft)
    {
        _onPacket = onPacket;
        _onClientLeft = onClientLeft;
    }

    public bool IsHost => _isHost;
    public bool HasClients => _clients.Count > 0;
    public bool IsClientConnected => _uplink != null;

    /// <summary>客机端上行链路对应的对端 UID（主机或中继服务端），由 MpWire 在握手完成后设置。</summary>
    public int UplinkUid => _uplinkUid;
    public void SetUplinkUid(int uid) => _uplinkUid = uid;

    public void StartHost(int port, bool ipv6)
    {
        Stop();
        _isHost = true;
        if (ipv6)
        {
            _listener = new TcpListener(IPAddress.IPv6Any, port);
            _listener.Server.DualMode = true;
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
        _uplinkUid = MpConstants.UnassignedUid;
        TcpClient tcp = IPAddress.TryParse(host, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6
            ? new TcpClient(AddressFamily.InterNetworkV6)
            : new TcpClient();
        tcp.ReceiveTimeout = timeoutMs;
        tcp.SendTimeout = 10_000;
        var ar = tcp.BeginConnect(host, port, null, null);
        if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
        {
            tcp.Dispose();
            throw new TimeoutException($"Connect to {host}:{port} timed out");
        }
        tcp.EndConnect(ar);
        _uplink = new Link { Tcp = tcp, Stream = tcp.GetStream() };
    }

    public void Enqueue(int? targetUid, int? exceptUid, byte[] framed, bool dropIfCongested)
    {
        if (_isHost)
        {
            // 指定 targetUid 时只发给该连接；找不到则丢弃，不能退化为广播
            // （RejectAction 等会在入队后立即 DisconnectClient，目标可能已离线）。
            if (targetUid is int uid)
            {
                if (_clients.TryGetValue(uid, out var one))
                    TryEnqueue(one, framed, dropIfCongested);
                return;
            }
            foreach (var kvp in _clients)
            {
                if (exceptUid is int ex && kvp.Key == ex) continue;
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
            _uplinkUid = MpConstants.UnassignedUid;
        }
        DisconnectAll();
        try { _listener?.Stop(); } catch { }
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
            PumpLink(_uplinkUid, _uplink);
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
                    _onPacket(uid, packet);
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
            _onClientLeft(uid);
        }
        else if (_uplink == link)
        {
            int uplinkUid = _uplinkUid;
            CloseLink(link);
            _uplink = null;
            _uplinkUid = MpConstants.UnassignedUid;
            _onClientLeft(uplinkUid);
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
        try { link.Stream?.Dispose(); } catch { }
        try { link.Tcp?.Close(); } catch { }
        link.Stream = null;
        link.Pending.Clear();
    }
}

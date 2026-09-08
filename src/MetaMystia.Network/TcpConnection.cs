using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;

using MemoryPack;

using MetaMystia.Protocol;

namespace MetaMystia.Network.Core;

public sealed record TransportEvent(Message? Message, string Error = "", int Bytes = 0);

public sealed class TcpConnection : IDisposable
{
    private const int MaxQueuedBytes = 8 * 1024 * 1024;
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<byte[]> _outgoing = Channel.CreateBounded<byte[]>(128);
    private readonly ConcurrentQueue<TransportEvent> _incoming = new();
    private int _incomingBytes;
    private int _outgoingBytes;
    private int _closed;
    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public TcpConnection(TcpClient client)
    {
        _client = client;
        client.NoDelay = true;
        _stream = client.GetStream();
        _ = ReadLoop();
        _ = WriteLoop();
    }

    public static async Task<TcpConnection> ConnectAsync(string address, int port, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
            return new(client);
        }
        catch { client.Dispose(); throw; }
    }

    public bool Send(Message message, bool closeAfterFlush = false)
    {
        if (IsClosed) return false;
        var data = MemoryPackSerializer.Serialize(message);
        if (data.Length > ProtocolVersion.MaxFrameBytes || Interlocked.Add(ref _outgoingBytes, data.Length) > MaxQueuedBytes
            || !_outgoing.Writer.TryWrite(data))
        { Close("Outgoing queue limit exceeded"); return false; }
        if (closeAfterFlush) _outgoing.Writer.TryComplete();
        return true;
    }

    public bool TryDequeue(out TransportEvent received)
    {
        if (!_incoming.TryDequeue(out received!)) return false;
        Interlocked.Add(ref _incomingBytes, -received.Bytes);
        return true;
    }

    private async Task ReadLoop()
    {
        try
        {
            var header = new byte[4];
            while (await ReadFull(header).ConfigureAwait(false))
            {
                int length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length is <= 0 or > ProtocolVersion.MaxFrameBytes) throw new InvalidDataException("Invalid frame length");
                var data = new byte[length];
                if (!await ReadFull(data).ConfigureAwait(false)) throw new EndOfStreamException("Incomplete frame");
                var message = MemoryPackSerializer.Deserialize<Message>(data) ?? throw new InvalidDataException("Empty message");
                if (Interlocked.Add(ref _incomingBytes, length) > MaxQueuedBytes || _incoming.Count >= 512)
                    throw new InvalidDataException("Incoming queue limit exceeded");
                _incoming.Enqueue(new(message, Bytes: length));
            }
            Close("Remote disconnected");
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException or MemoryPackSerializationException)
        { Close(error.Message); }
    }

    private async Task<bool> ReadFull(byte[] buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int count = await _stream.ReadAsync(buffer.AsMemory(read), _stop.Token).ConfigureAwait(false);
            if (count == 0) return false;
            read += count;
        }
        return true;
    }

    private async Task WriteLoop()
    {
        try
        {
            var header = new byte[4];
            await foreach (var data in _outgoing.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);
                await _stream.WriteAsync(header, _stop.Token).ConfigureAwait(false);
                await _stream.WriteAsync(data, _stop.Token).ConfigureAwait(false);
                Interlocked.Add(ref _outgoingBytes, -data.Length);
            }
            Close("Connection closed");
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        { Close(error.Message); }
    }

    private void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        _stop.Cancel();
        _outgoing.Writer.TryComplete();
        _client.Dispose();
        _incoming.Enqueue(new(null, reason));
    }

    public void Dispose() => Close("Connection stopped");
}

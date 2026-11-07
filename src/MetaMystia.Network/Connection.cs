using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MetaMystia.Network;

internal sealed class Connection
{
    private readonly TcpClient tcp;
    private readonly TimeSpan timeout;
    private readonly Channel<byte[]> output = Channel.CreateBounded<byte[]>(Protocol.QueueCapacity);
    private readonly CancellationTokenSource stopped = new();
    private readonly Action<Frame> received;
    private readonly Action<NetworkError> ended;
    private int closed;
    internal Task Completion { get; private set; } = Task.CompletedTask;

    internal Connection(TcpClient tcp, TimeSpan timeout, Action<Frame> received, Action<NetworkError> ended)
    { this.tcp = tcp; this.timeout = timeout; this.received = received; this.ended = ended; tcp.NoDelay = true; }

    internal void Start() => Completion = Task.WhenAll(ReadLoop(), WriteLoop());
    internal bool Send(Frame frame) => SendBytes(Protocol.Encode(frame));
    internal bool SendBytes(byte[] bytes)
    {
        if (Volatile.Read(ref closed) != 0) return false;
        if (output.Writer.TryWrite(bytes)) return true;
        Close(NetworkErrorCode.SendQueueFull);
        return false;
    }

    internal void Finish() => output.Writer.TryComplete();
    internal void Close(NetworkError reason)
    {
        if (Interlocked.Exchange(ref closed, 1) != 0) return;
        stopped.Cancel(); output.Writer.TryComplete(); tcp.Dispose(); ended(reason);
    }

    private async Task ReadLoop()
    {
        try
        {
            var stream = tcp.GetStream();
            var header = new byte[4];
            while (!stopped.IsCancellationRequested)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
                deadline.CancelAfter(timeout);
                await ReadExactly(stream, header, deadline.Token).ConfigureAwait(false);
                int size = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (size < 1 || size > Protocol.MaxFrame) throw new InvalidDataException("Invalid frame length");
                var bytes = new byte[size];
                await ReadExactly(stream, bytes, deadline.Token).ConfigureAwait(false);
                received(Protocol.Decode(bytes));
            }
        }
        catch (Exception e)
        { Close(e is OperationCanceledException ? NetworkErrorCode.ReceiveTimeout : NetworkErrorCode.ConnectionLost); }
    }

    internal static async Task ReadExactly(NetworkStream stream, Memory<byte> bytes, CancellationToken token)
    {
        while (!bytes.IsEmpty)
        {
            int n = await stream.ReadAsync(bytes, token).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException();
            bytes = bytes[n..];
        }
    }

    private async Task WriteLoop()
    {
        try
        {
            var stream = tcp.GetStream();
            await foreach (var bytes in output.Reader.ReadAllAsync(stopped.Token).ConfigureAwait(false))
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
                deadline.CancelAfter(timeout);
                await stream.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
            }
            Close(NetworkErrorCode.Finished);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        { Close(NetworkErrorCode.ConnectionLost); }
    }
}

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace MetaMystia.Network;

// 握手后由 WebSocket 负责分帧与掩码，本层仍按原有的长度前缀帧收发。
internal sealed class Connection
{
    // RFC 6455 固定 GUID，用于校验握手应答。
    private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaxHandshakeBytes = 8192;
    private readonly TcpClient tcp;
    private readonly bool server;
    private readonly TimeSpan timeout;
    private readonly Channel<byte[]> output = Channel.CreateBounded<byte[]>(Protocol.QueueCapacity);
    private readonly CancellationTokenSource stopped = new();
    private readonly Action<Frame> received;
    private readonly Action<NetworkError> ended;
    private WebSocket? socket;
    private int closed;
    internal Task Completion { get; private set; } = Task.CompletedTask;

    internal Connection(TcpClient tcp, bool server, TimeSpan timeout, Action<Frame> received, Action<NetworkError> ended)
    { this.tcp = tcp; this.server = server; this.timeout = timeout; this.received = received; this.ended = ended; tcp.NoDelay = true; }

    internal void Start() => Completion = Run();
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
        stopped.Cancel(); output.Writer.TryComplete(); socket?.Dispose(); tcp.Dispose(); ended(reason);
    }

    // 升级握手先于收发；保活仍由协议层的 Ping/Pong 负责，不使用 WebSocket 保活。
    private async Task Run()
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
            deadline.CancelAfter(timeout);
            var stream = tcp.GetStream();
            if (server) await AcceptUpgrade(stream, deadline.Token).ConfigureAwait(false);
            else await RequestUpgrade(stream, deadline.Token).ConfigureAwait(false);
            socket = WebSocket.CreateFromStream(stream, server, null, Timeout.InfiniteTimeSpan);
        }
        catch (Exception e)
        { Close(e is OperationCanceledException ? NetworkErrorCode.ReceiveTimeout : NetworkErrorCode.ConnectionLost); return; }
        await Task.WhenAll(ReadLoop(), WriteLoop()).ConfigureAwait(false);
    }

    private async Task AcceptUpgrade(NetworkStream stream, CancellationToken token)
    {
        var request = await ReadUpgrade(stream, token).ConfigureAwait(false);
        if (Header(request, "Sec-WebSocket-Key") is not { } key)
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n"), token).ConfigureAwait(false);
            throw new InvalidDataException("Not a WebSocket upgrade");
        }
        var response = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {Accept(key)}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), token).ConfigureAwait(false);
    }

    private async Task RequestUpgrade(NetworkStream stream, CancellationToken token)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var request = $"GET / HTTP/1.1\r\nHost: {tcp.Client.RemoteEndPoint}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), token).ConfigureAwait(false);
        var response = await ReadUpgrade(stream, token).ConfigureAwait(false);
        if (!response.StartsWith("HTTP/1.1 101", StringComparison.Ordinal) || Header(response, "Sec-WebSocket-Accept") != Accept(key))
            throw new InvalidDataException("WebSocket upgrade rejected");
    }

    // 逐字节读取握手，避免读入紧随其后的 WebSocket 帧。
    private static async Task<string> ReadUpgrade(NetworkStream stream, CancellationToken token)
    {
        var text = new StringBuilder();
        var one = new byte[1];
        while (text.Length < MaxHandshakeBytes)
        {
            if (await stream.ReadAsync(one, token).ConfigureAwait(false) == 0) throw new EndOfStreamException();
            text.Append((char)one[0]);
            if (text.Length >= 4 && text[^4] == '\r' && text[^3] == '\n' && text[^2] == '\r' && text[^1] == '\n') return text.ToString();
        }
        throw new InvalidDataException("Upgrade header too large");
    }

    private static string? Header(string text, string name)
    {
        foreach (var line in text.Split("\r\n"))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && line.AsSpan(0, colon).Equals(name, StringComparison.OrdinalIgnoreCase)) return line[(colon + 1)..].Trim();
        }
        return null;
    }

    private static string Accept(string key) => Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + HandshakeGuid)));

    private async Task ReadLoop()
    {
        var ws = socket!;
        try
        {
            var header = new byte[4];
            while (!stopped.IsCancellationRequested)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
                deadline.CancelAfter(timeout);
                await ReadExactly(ws, header, deadline.Token).ConfigureAwait(false);
                int size = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (size < 1 || size > Protocol.MaxFrame) throw new InvalidDataException("Invalid frame length");
                var bytes = new byte[size];
                await ReadExactly(ws, bytes, deadline.Token).ConfigureAwait(false);
                received(Protocol.Decode(bytes));
            }
        }
        catch (Exception e)
        { Close(e is OperationCanceledException ? NetworkErrorCode.ReceiveTimeout : NetworkErrorCode.ConnectionLost); }
    }

    // WebSocket 的消息边界对本层透明：按长度前缀连续读取字节流。
    internal static async Task ReadExactly(WebSocket ws, Memory<byte> bytes, CancellationToken token)
    {
        while (!bytes.IsEmpty)
        {
            var result = await ws.ReceiveAsync(bytes, token).ConfigureAwait(false);
            if (result.Count == 0) throw new EndOfStreamException();
            if (result.MessageType != WebSocketMessageType.Binary) throw new InvalidDataException("Unexpected message type");
            bytes = bytes[result.Count..];
        }
    }

    private async Task WriteLoop()
    {
        var ws = socket!;
        try
        {
            await foreach (var bytes in output.Reader.ReadAllAsync(stopped.Token).ConfigureAwait(false))
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
                deadline.CancelAfter(timeout);
                await ws.SendAsync(bytes, WebSocketMessageType.Binary, true, deadline.Token).ConfigureAwait(false);
            }
            Close(NetworkErrorCode.Finished);
        }
        catch (Exception e) when (e is IOException or SocketException or WebSocketException or OperationCanceledException or ObjectDisposedException)
        { Close(NetworkErrorCode.ConnectionLost); }
    }
}

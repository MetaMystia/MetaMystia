using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using MetaMystia.Discovery;
using MetaMystia.Gateway;
using MetaMystia.Network;

static class GatewayChecks
{
    private static int count;
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }

    private static byte[] Hello(string game = "wire-game", string mod = "wire-mod", int protocol = 123)
    {
        using var body = new MemoryStream();
        using var writer = new BinaryWriter(body, Encoding.UTF8);
        writer.Write((byte)1); writer.Write(0x3152494d); writer.Write(protocol);
        writer.Write(game); writer.Write(mod); writer.Write("original-key");
        writer.Write(new byte[] { 255, 7, 8, 9, 0, 128 });
        var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, (int)body.Length);
        body.ToArray().CopyTo(frame, 4);
        return frame;
    }

    private static async Task<byte[]> Read(NetworkStream stream, int length)
    {
        var bytes = new byte[length];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Connection.ReadExactly(stream, bytes, deadline.Token);
        return bytes;
    }

    private static async Task<NetworkErrorCode> Rejection(IPEndPoint endpoint, byte[] hello)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(endpoint);
        var stream = client.GetStream();
        await stream.WriteAsync(hello);
        int size = BinaryPrimitives.ReadInt32LittleEndian(await Read(stream, 4));
        var reply = await Read(stream, size);
        Assert(reply[0] == 2, "拒绝消息沿用现有帧类型");
        return (NetworkErrorCode)BinaryPrimitives.ReadUInt16LittleEndian(reply.AsSpan(1));
    }

    internal static async Task Run()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var directory = builder.Build();
        var backend = new TcpListener(IPAddress.Loopback, 0);
        backend.Start();
        var backendEndpoint = (IPEndPoint)backend.LocalEndpoint;
        var catalog = new ServerCatalog([new("wire", "wire-game", "wire-mod", "ready", "127.0.0.1", backendEndpoint.Port, null)]);
        directory.MapGet("/internal/routes", () => Results.Json(catalog));
        await directory.StartAsync();
        using var routes = new RouteCache(directory.Urls.Single(), "test") { Lifetime = TimeSpan.FromMilliseconds(300) };
        await using var gateway = new GatewayServer(new(IPAddress.Loopback, 0), routes)
        { HandshakeTimeout = TimeSpan.FromMilliseconds(500) };
        gateway.Start();
        Assert(await Rejection(gateway.Endpoint, Hello()) == NetworkErrorCode.DirectoryUnavailable, "路由未加载时明确拒绝新连接");
        await routes.RefreshAsync(CancellationToken.None);
        Assert(await Rejection(gateway.Endpoint, Hello(mod: "unknown")) == NetworkErrorCode.VersionUnavailable, "未知模组版本不转到最新版");
        Assert(await Rejection(gateway.Endpoint, Hello(game: "unknown")) == NetworkErrorCode.VersionUnavailable, "不同游戏版本不交叉转发");
        var invalid = Hello(); invalid[5] = 0;
        Assert(await Rejection(gateway.Endpoint, invalid) == NetworkErrorCode.InvalidHello, "错误握手标识被拒绝");
        await routes.RefreshAsync(CancellationToken.None);
        using var player = new TcpClient();
        await player.ConnectAsync(gateway.Endpoint);
        var stream = player.GetStream();
        var hello = Hello();
        await stream.WriteAsync(hello.AsMemory(0, 2));
        await stream.WriteAsync(hello.AsMemory(2, 9));
        await stream.WriteAsync(hello.AsMemory(11));
        using var accepted = await backend.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var destination = accepted.GetStream();
        Assert((await Read(destination, hello.Length)).SequenceEqual(hello), "拆包后的 Hello 逐字节保持原样，未知玩家格式和协议号不由入口重写");
        var data = Enumerable.Range(0, 65536).Select(i => (byte)i).ToArray();
        await stream.WriteAsync(data);
        Assert((await Read(destination, data.Length)).SequenceEqual(data), "大于转发缓冲区的客户端数据完整到达后端");
        await destination.WriteAsync(data);
        Assert((await Read(stream, data.Length)).SequenceEqual(data), "后端数据完整返回客户端");
        await Task.Delay(350);
        Assert(await Rejection(gateway.Endpoint, Hello()) == NetworkErrorCode.DirectoryUnavailable, "过期路由不接纳新连接");
        await destination.WriteAsync(new byte[] { 42 });
        Assert((await Read(stream, 1))[0] == 42, "路由过期不影响已有转发连接");
        accepted.Dispose();
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            Assert(await stream.ReadAsync(new byte[1], deadline.Token) == 0, "后端断开后客户端连接及时关闭");

        using (var partial = new TcpClient())
        {
            await partial.ConnectAsync(gateway.Endpoint);
            await partial.GetStream().WriteAsync(new byte[] { 10, 0 });
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Assert(await partial.GetStream().ReadAsync(new byte[1], deadline.Token) == 0, "不完整握手超时释放连接");
        }
        using (var oversized = new TcpClient())
        {
            await oversized.ConnectAsync(gateway.Endpoint);
            await oversized.GetStream().WriteAsync(BitConverter.GetBytes(256 * 1024 + 1));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Assert(await oversized.GetStream().ReadAsync(new byte[1], deadline.Token) == 0, "超长首帧在分配正文前关闭");
        }
        await using (var limited = new GatewayServer(new(IPAddress.Loopback, 0), routes, 1))
        {
            limited.Start();
            using var holding = new TcpClient(); await holding.ConnectAsync(limited.Endpoint);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (limited.ConnectionCount != 1) await Task.Delay(10, deadline.Token);
            using var excess = new TcpClient(); await excess.ConnectAsync(limited.Endpoint);
            Assert(await excess.GetStream().ReadAsync(new byte[1], deadline.Token) == 0, "入口连接总数受到限制");
        }
        await ProcessGateway(directory.Urls.Single(), backend, hello);
        backend.Stop();
        await routes.RefreshAsync(CancellationToken.None);
        Assert(await Rejection(gateway.Endpoint, hello) == NetworkErrorCode.ConnectFailed, "后端连接失败返回明确拒绝");
        await directory.StopAsync();
        Console.WriteLine($"ALL PASS ({count} gateway assertions)");
    }

    private static async Task ProcessGateway(string directoryUrl, TcpListener backend, byte[] hello)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "MetaMystia.Gateway.exe" : "MetaMystia.Gateway"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["MM_GATEWAY_ADDRESS"] = "127.0.0.1";
        start.Environment["MM_GATEWAY_PORT"] = port.ToString();
        start.Environment["MM_DIRECTORY_URL"] = directoryUrl;
        start.Environment["MM_INSTANCE_TOKEN"] = "test";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                using var probe = new TcpClient();
                try
                {
                    await probe.ConnectAsync(IPAddress.Loopback, port, deadline.Token);
                    await probe.GetStream().WriteAsync(hello, deadline.Token);
                    var accept = backend.AcceptTcpClientAsync(deadline.Token).AsTask();
                    using var connected = await accept;
                    Assert((await Read(connected.GetStream(), hello.Length)).SequenceEqual(hello), "独立 Gateway 进程加载 HTTP 路由并原样转发握手");
                    break;
                }
                catch (SocketException) { await Task.Delay(50, deadline.Token); }
            }
        }
        finally
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync();
            await output; await errors;
        }
    }
}

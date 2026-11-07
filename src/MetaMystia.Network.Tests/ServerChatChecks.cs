using System.Net.Sockets;

using MetaMystia.Hosting;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

static partial class Checks
{
    static async Task ServerChat()
    {
        await using var server = new Server(new() { Messages = GameMessageRules.Create(), ChatFilter = new() { Enabled = true, Words = ["blocked"] } });
        await server.StartAsync();
        var a = await Connect(server, "say-a");
        var b = await Connect(server, "say-b");
        var c = await Connect(server, "say-c");
        await Pump(a.CreateRoomAsync()); await Pump(b.CreateRoomAsync());
        var messages = new List<ReceivedMessage>();
        MultiplayerMessage.Delivered.Clear();
        foreach (var client in new[] { a, b, c })
            client.MessageReceived += m =>
            {
                messages.Add(m);
                GameSession.Client = client;
                GameMessages.Receive(m);
            };
        var output = new List<string>();
        await ServerCommands.Execute("  /SAY 大家好  hello world", server, (_, text) => output.Add(text));
        await Until(() => messages.Count == 3);
        Assert(messages.All(m => m.Context.Sender == 0 && m.Context.Room == 0
            && Protocol.Read<ChatPayload>(m.Body).Message == "大家好  hello world")
            && MultiplayerMessage.Delivered.Count == 3 && MultiplayerMessage.Delivered.All(m => m.Sender == 0),
            "say 命令保留正文空格，以服务器身份送达不同房间和大厅，实际客户端入口可解析");
        Assert(await server.SayAsync("blocked") == NetworkErrorCode.ChatFiltered
            && await server.SayAsync("  ") == NetworkErrorCode.InvalidChat
            && await server.SayAsync(new string('a', 1025)) == NetworkErrorCode.InvalidChat, "服务端发言同样检查敏感词、空白及长度");
        await ServerCommands.Execute("say", server, (_, text) => output.Add(text));
        Assert(output[^1].StartsWith("用法：say"), "空 say 命令显示用法");
        Assert(await server.SayAsync(new string('好', 1024)) == NetworkErrorCode.None, "允许最长合法服务端发言");
        await Until(() => messages.Count >= 6);
        Assert(messages.Count == 6 && a.IsConnected && b.IsConnected && c.IsConnected, "拒绝的服务端发言不广播且不影响连接");

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(server.Endpoint);
        await tcp.GetStream().WriteAsync(Protocol.Encode(new(Kind.Hello, Protocol.Hello(Versions.Current, Player("fake-server"), ""))));
        var welcome = await Read(tcp);
        int uid = welcome.Sender;
        await tcp.GetStream().WriteAsync(Protocol.Encode(new(Kind.Data, Protocol.Pack(new ChatPayload { Message = "fake" }),
            Sender: 0, Type: (ushort)GameMessageType.Chat, Route: Route.World)));
        await Until(() => messages.Count >= 9);
        Assert(uid != 0 && messages.Skip(6).All(m => m.Context.Sender == uid), "客户端伪造 UID 0 仍被服务端改回真实玩家身份");
        a.Disconnect(); b.Disconnect(); c.Disconnect();
        await server.StopAsync();
        Assert(await server.SayAsync("stopped") == NetworkErrorCode.ServerStopped, "关闭后发言立即返回停止状态");
    }
}

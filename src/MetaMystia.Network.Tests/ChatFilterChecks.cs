using System.Text.Json;

using MemoryPack;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

static partial class Checks
{
    static async Task ChatFiltering()
    {
        var filter = JsonSerializer.Deserialize<ChatFilterOptions>(
            """{"enabled":true,"ignoreCase":true,"words":["敏感词","blocked","", "   "]}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        await using var server = new Server(new() { Messages = GameMessageRules.Create(), ChatFilter = filter });
        await server.StartAsync();
        var sender = await Connect(server, "chat-sender");
        var sameRoom = await Connect(server, "chat-member");
        var otherRoom = await Connect(server, "chat-other");
        await Pump(sender.CreateRoomAsync());
        await Pump(sender.SetJoinableAsync(true));
        await Pump(sameRoom.JoinRoomAsync(sender.State.Room!.Id));
        await Pump(otherRoom.CreateRoomAsync());
        var received = new List<(int Recipient, ReceivedMessage Message)>();
        var errors = new List<NetworkError>();
        int otherErrors = 0;
        sender.ChatRejected += errors.Add;
        sameRoom.ChatRejected += _ => otherErrors++;
        otherRoom.ChatRejected += _ => otherErrors++;
        foreach (var client in new[] { sender, sameRoom, otherRoom })
            client.MessageReceived += m => received.Add((client.Uid, m));

        GameSession.Client = sender;
        GameMessages.Send(new ChatMessage { Message = "正常聊天" });
        await Until(() => received.Count == 3);
        Assert(received.All(x => x.Message.Context.Sender == sender.Uid
            && MemoryPackSerializer.Deserialize<ChatPayload>(x.Message.Body)!.Message == "正常聊天")
            && received.Select(x => x.Recipient).Distinct().Count() == 3,
            "实际聊天发送入口使用共享正文，跨房广播且向本人回送一次");
        received.Clear();
        foreach (var text in new[] { "前缀敏感词后缀", "xxBLOCKEDyy" })
        {
            int count = errors.Count;
            sender.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = text }));
            await Until(() => errors.Count == count + 1);
            Assert(errors[^1].Code == NetworkErrorCode.ChatFiltered, "包含匹配及忽略大小写：" + text);
        }
        foreach (var body in new[]
        {
            Array.Empty<byte>(), Protocol.Pack(new ChatPayload { Message = null! }),
            Protocol.Pack(new ChatPayload { Message = "  " }),
            Protocol.Pack(new ChatPayload { Message = new string('a', 1025) }), new byte[4097]
        })
        {
            int count = errors.Count;
            sender.SendToWorld((ushort)GameMessageType.Chat, body);
            await Until(() => errors.Count == count + 1);
            Assert(errors[^1].Code == NetworkErrorCode.InvalidChat, "无效聊天正文只拒绝消息");
        }
        sender.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = new string('好', 1024) }));
        await Until(() => received.Count >= 3);
        Assert(received.Count == 3 && otherErrors == 0 && sender.IsConnected && sameRoom.IsConnected && otherRoom.IsConnected
            && received.All(x => MemoryPackSerializer.Deserialize<ChatPayload>(x.Message.Body)!.Message.Length == 1024),
            "拒绝内容未泄露、提示只发本人，后续最长合法聊天正常送达且全员在线");
        foreach (var client in new[] { sender, sameRoom, otherRoom }) client.Disconnect();

        foreach (var options in new[]
        {
            new ChatFilterOptions { Enabled = false, Words = ["blocked"] },
            new ChatFilterOptions { Enabled = true, IgnoreCase = false, Words = ["blocked"] },
            new ChatFilterOptions { Enabled = true, Words = ["", " "] }
        })
        {
            await using var configured = new Server(new() { Messages = GameMessageRules.Create(), ChatFilter = options });
            await configured.StartAsync();
            var client = await Connect(configured, "config-check");
            int accepted = 0, rejected = 0;
            client.MessageReceived += _ => accepted++;
            client.ChatRejected += _ => rejected++;
            client.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = "BLOCKED" }));
            await Until(() => accepted == 1);
            Assert(rejected == 0, "关闭过滤、大小写敏感及空词表配置允许相应消息");
            if (options.Enabled && !options.IgnoreCase)
            {
                client.SendToWorld((ushort)GameMessageType.Chat, Protocol.Pack(new ChatPayload { Message = "blocked" }));
                await Until(() => rejected == 1);
                Assert(client.IsConnected, "大小写敏感模式仍拦截精确词条");
            }
            client.Disconnect();
        }
    }
}

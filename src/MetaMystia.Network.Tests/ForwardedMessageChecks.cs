using MemoryPack;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

static partial class Checks
{
    static async Task ForwardedMessageChecks()
    {
        var failures = new List<string>();
        byte[] valid = MemoryPackSerializer.Serialize<MultiplayerMessage>(new ChatMessage());
        byte[] validRoom = MemoryPackSerializer.Serialize<MultiplayerMessage>(new NightCookMessage());
        byte[] mismatch = MemoryPackSerializer.Serialize<MultiplayerMessage>(new PingMessage());
        byte[] nil = MemoryPackSerializer.Serialize<MultiplayerMessage>(null);
        foreach (bool room in new[] { false, true })
        foreach (var (name, body) in new (string, byte[])[]
        {
            ("截断正文", []), ("未知联合类型", [250, 0]), ("空消息", nil), ("内外类型不一致", mismatch)
        })
        {
            await using var server = new Server(new() { Messages = GameMessageRules.Create() });
            await server.StartAsync();
            var recipient = await Connect(server, "recipient");
            var attacker = await Connect(server, "sender");
            var observer = await Connect(server, "observer");
            if (room)
            {
                await Pump(recipient.CreateRoomAsync());
                await Pump(recipient.SetJoinableAsync(true));
                await Pump(attacker.JoinRoomAsync(recipient.State.Room!.Id));
            }
            int received = 0, callbacks = 0;
            MultiplayerMessage.Delivered.Clear();
            foreach (var client in new[] { recipient, observer })
            {
                client.CallbackError += _ => callbacks++;
                client.MessageReceived += message =>
                {
                    received++;
                    GameSession.Client = client;
                    GameMessages.Receive(message);
                };
            }
            if (room) attacker.SendToRoom((ushort)GameMessageType.NightCook, body);
            else attacker.SendToWorld((ushort)GameMessageType.Chat, body);
            await Until(() => received == (room ? 1 : 2));
            bool survived = recipient.IsConnected && observer.IsConnected;
            string scenario = $"{(room ? "房间" : "世界")}转发{name}";
            Console.WriteLine($"CHECK {scenario}: 接收者在线={survived}, 回调异常={callbacks}");
            if (!survived || callbacks != 0 || MultiplayerMessage.Delivered.Count != 0)
                failures.Add(scenario);
            else
            {
                if (room)
                {
                    attacker.SendToRoom((ushort)GameMessageType.NightCook, validRoom);
                    await Until(() => MultiplayerMessage.Delivered.Count == 1);
                    Assert(MultiplayerMessage.Delivered[0] == (recipient.Uid, attacker.Uid),
                        scenario + "后正常房间消息仍只送达房内玩家");
                    MultiplayerMessage.Delivered.Clear();
                }
                attacker.SendToWorld((ushort)GameMessageType.Chat, valid);
                await Until(() => MultiplayerMessage.Delivered.Count == 2);
                Assert(MultiplayerMessage.Delivered.All(x => x.Sender == attacker.Uid)
                    && recipient.IsConnected && observer.IsConnected,
                    scenario + "被丢弃后仍可接收正常消息且保留真实来源");
            }
            recipient.Disconnect(); attacker.Disconnect(); observer.Disconnect();
        }
        Assert(failures.Count == 0, "非法转发正文不应中断接收者会话：" + string.Join("、", failures));
    }
}

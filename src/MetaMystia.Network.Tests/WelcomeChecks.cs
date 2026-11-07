using System.Text.Json;

using MetaMystia.Hosting;
using MetaMystia.Network;

static partial class Checks
{
    static async Task WelcomeAndReload()
    {
        await using var server = new Server(new() { Messages = GameMessageRules.Create(), WelcomeMessages = ["欢迎", "规则"] });
        await server.StartAsync();
        async Task<Client> Enter(string name, List<ReceivedMessage> received)
        {
            var client = new Client(); clients.Add(client);
            client.MessageReceived += received.Add;
            await Pump(client.ConnectAsync(server.Endpoint, Player(name)));
            return client;
        }
        var firstMessages = new List<ReceivedMessage>();
        var first = await Enter("welcome-first", firstMessages);
        await Until(() => firstMessages.Count == 2);
        Assert(firstMessages.Select(m => Protocol.Read<ChatPayload>(m.Body).Message).SequenceEqual(new[] { "欢迎", "规则" })
            && firstMessages.All(m => m.Context.Sender == 0), "连接成功后按顺序收到服务器欢迎公告");
        var secondMessages = new List<ReceivedMessage>();
        var second = await Enter("welcome-second", secondMessages);
        await Until(() => secondMessages.Count == 2);
        await Pump(first.CreateRoomAsync()); await Pump(first.SetJoinableAsync(true)); await Pump(second.JoinRoomAsync(first.State.Room!.Id));
        Assert(firstMessages.Count == 2 && secondMessages.Count == 2, "公告只给新连接，不广播给老玩家且入房不重复");

        var directory = Path.Combine(Directory.GetCurrentDirectory(), ".tmp", "reload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var serverPath = Path.Combine(directory, "server.json");
        var filterPath = Path.Combine(directory, "chat-filter.json");
        var output = new List<string>();
        var logs = new System.Collections.Concurrent.ConcurrentQueue<ServerLogEntry>();
        server.Logged += logs.Enqueue;
        try
        {
            await File.WriteAllTextAsync(serverPath, """{"logChat":true,"welcomeMessages":["新版欢迎"]}""");
            await File.WriteAllTextAsync(filterPath, """{"enabled":true,"words":["blocked"]}""");
            await ServerCommands.Execute("reload", server, (_, text) => output.Add(text), directory);
            Assert(output[^1].Contains("已生效"), "reload 读取两个 JSON 并应用配置");
            var thirdMessages = new List<ReceivedMessage>();
            var third = await Enter("welcome-third", thirdMessages);
            await Until(() => thirdMessages.Count == 1);
            Assert(Protocol.Read<ChatPayload>(thirdMessages[0].Body).Message == "新版欢迎" && firstMessages.Count == 2,
                "热更新只影响后续连接的公告，已有连接不重复接收");
            Assert(await server.SayAsync("blocked") == NetworkErrorCode.ChatFiltered, "热更新敏感词即时生效");
            await server.SayAsync("log-marker");
            await Until(() => thirdMessages.Count == 2);
            Assert(logs.Any(e => e.Level == ServerLogLevel.Chat && e.Message.Contains("log-marker")), "热更新聊天日志开关即时生效");

            await File.WriteAllTextAsync(serverPath, """{"logChat":false,"welcomeMessages":["不能部分应用"]}""");
            await File.WriteAllTextAsync(filterPath, "{invalid");
            await ServerCommands.Execute("reload", server, (_, text) => output.Add(text), directory);
            Assert(output[^1].Contains("保留原配置") && await server.SayAsync("blocked") == NetworkErrorCode.ChatFiltered,
                "任一文件无效时重载失败且原词表保持生效");
            var fourthMessages = new List<ReceivedMessage>();
            var fourth = await Enter("welcome-fourth", fourthMessages);
            await Until(() => fourthMessages.Count == 1);
            Assert(Protocol.Read<ChatPayload>(fourthMessages[0].Body).Message == "新版欢迎", "失败重载不会部分替换公告");
            await server.SayAsync("still-logged");
            await Until(() => fourthMessages.Count == 2);
            Assert(logs.Any(e => e.Level == ServerLogLevel.Chat && e.Message.Contains("still-logged")), "失败重载保留原日志开关");
            await File.WriteAllTextAsync(filterPath, "{}");
            foreach (var invalid in new object?[] { null, new[] { "" }, new[] { new string('a', 1025) }, Enumerable.Repeat("x", 17).ToArray() })
            {
                await File.WriteAllTextAsync(serverPath, JsonSerializer.Serialize(new { welcomeMessages = invalid }));
                await ServerCommands.Execute("reload", server, (_, text) => output.Add(text), directory);
                Assert(output[^1].Contains("保留原配置"), "无效公告数组不能替换运行配置");
            }
            File.Delete(serverPath); File.Delete(filterPath);
            await ServerCommands.Execute("reload", server, (_, text) => output.Add(text), directory);
            var fifthMessages = new List<ReceivedMessage>();
            var fifth = await Enter("welcome-fifth", fifthMessages);
            await server.SayAsync("blocked");
            await Until(() => fifthMessages.Count > 0);
            Assert(fifthMessages.Count == 1 && Protocol.Read<ChatPayload>(fifthMessages[0].Body).Message == "blocked",
                "缺少配置恢复默认值：无欢迎公告且不启用过滤");
            foreach (var client in new[] { first, second, third, fourth, fifth }) client.Disconnect();
        }
        finally
        {
            File.Delete(serverPath); File.Delete(filterPath); Directory.Delete(directory);
        }
    }
}

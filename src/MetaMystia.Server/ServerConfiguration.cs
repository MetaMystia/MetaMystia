using System.Text.Json;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

internal static class ServerConfiguration
{
    private static readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };

    public static ServerTextSettings Load(string directory)
    {
        var serverPath = Path.Combine(directory, "server.json");
        var filterPath = Path.Combine(directory, "chat-filter.json");
        var config = File.Exists(serverPath)
            ? JsonSerializer.Deserialize<ConsoleOptions>(File.ReadAllText(serverPath), json)
                ?? throw new InvalidDataException("服务端配置不能为 null。")
            : new ConsoleOptions();
        var filter = File.Exists(filterPath)
            ? JsonSerializer.Deserialize<ChatFilterOptions>(File.ReadAllText(filterPath), json)
                ?? throw new InvalidDataException("聊天过滤配置不能为 null。")
            : new ChatFilterOptions();
        var settings = new ServerTextSettings { ChatFilter = filter, LogChat = config.LogChat, WelcomeMessages = config.WelcomeMessages };
        settings.Validate();
        return settings;
    }

    private sealed record ConsoleOptions
    {
        public bool LogChat { get; init; }
        public string[] WelcomeMessages { get; init; } = [];
    }
}

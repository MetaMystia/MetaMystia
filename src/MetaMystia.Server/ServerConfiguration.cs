using System.Text.Json;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

internal static class ServerConfiguration
{
    private static readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };

    public static ServerTextSettings Load(string directory, string? name = null, string? content = null)
    {
        var serverPath = Path.Combine(directory, "server.json");
        var filterPath = Path.Combine(directory, "chat-filter.json");
        var config = name == "server" || File.Exists(serverPath)
            ? JsonSerializer.Deserialize<ConsoleOptions>(name == "server" ? content! : File.ReadAllText(serverPath), json)
                ?? throw new InvalidDataException("服务端配置不能为 null。")
            : new ConsoleOptions();
        var filter = name == "chat-filter" || File.Exists(filterPath)
            ? JsonSerializer.Deserialize<ChatFilterOptions>(name == "chat-filter" ? content! : File.ReadAllText(filterPath), json)
                ?? throw new InvalidDataException("聊天过滤配置不能为 null。")
            : new ChatFilterOptions();
        var settings = new ServerTextSettings { ChatFilter = filter, LogChat = config.LogChat, WelcomeMessages = config.WelcomeMessages };
        settings.Validate();
        return settings;
    }

    public static string Read(string directory, string name)
    {
        var path = Path.Combine(directory, name + ".json");
        return File.Exists(path) ? File.ReadAllText(path) : name == "server"
            ? "{\"logChat\":false,\"welcomeMessages\":[]}" : JsonSerializer.Serialize(new ChatFilterOptions(), json);
    }

    public static void Save(string directory, string name, string content)
    {
        var path = Path.Combine(directory, name + ".json");
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record ConsoleOptions
    {
        public bool LogChat { get; init; }
        public string[] WelcomeMessages { get; init; } = [];
    }
}

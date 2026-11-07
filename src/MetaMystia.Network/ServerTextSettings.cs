namespace MetaMystia.Network;

public sealed record ServerTextSettings
{
    public ChatFilterOptions ChatFilter { get; init; } = new();
    public bool LogChat { get; init; }
    public string[] WelcomeMessages { get; init; } = [];

    public void Validate()
    {
        if (ChatFilter?.Words == null || ChatFilter.Words.Any(word => word == null))
            throw new ArgumentException("敏感词 words 必须为字符串数组。");
        if (WelcomeMessages == null || WelcomeMessages.Length > 16
            || WelcomeMessages.Any(message => string.IsNullOrWhiteSpace(message) || message.Length > ChatPayload.MaxLength))
            throw new ArgumentException("welcomeMessages 最多 16 条，每条须为非空文本且不超过 1024 个 UTF-16 单元。");
    }
}

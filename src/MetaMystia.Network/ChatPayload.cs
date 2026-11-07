using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public sealed partial record ChatPayload
{
    public const int MaxLength = 1024;
    public string Message { get; init; } = "";
}

public sealed record ChatFilterOptions
{
    public bool Enabled { get; init; }
    public bool IgnoreCase { get; init; } = true;
    public string[] Words { get; init; } = [];
}

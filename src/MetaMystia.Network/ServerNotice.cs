using MemoryPack;

namespace MetaMystia.Network;

public enum ServerNoticeCode : ushort { Retiring = 1 }

[MemoryPackable]
public sealed partial record ServerNotice
{
    public ServerNoticeCode Code { get; init; }
    public string NoticeId { get; init; } = "";
    public string TargetModVersion { get; init; } = "";
}

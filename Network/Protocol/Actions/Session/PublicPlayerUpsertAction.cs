using MemoryPack;

namespace MetaMystia.Network;

/// <summary>服务端端点 -> 全服：公域层轻量玩家增量。</summary>
[MemoryPackable]
public partial class PublicPlayerUpsertAction : NetAction
{
    public PlayerLiteData Player { get; set; }
}

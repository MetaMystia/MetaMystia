using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 服务端端点 -> 所有人：公域层轻量玩家增量。
/// </summary>
[MemoryPackable]
public partial class PlayerPresenceAction : NetAction
{
    public PlayerLiteData Player { get; set; }
}

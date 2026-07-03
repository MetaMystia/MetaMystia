using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 服务端端点 -> 客机：握手确认，携带分配的 UID 和全服轻量玩家表。
/// </summary>
[MemoryPackable]
public partial class HelloAckAction : NetAction
{
    public int AssignedUid { get; set; }
    public PlayerLiteData[] Players { get; set; } = [];
}

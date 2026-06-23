using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 服务端端点 -> 客机：分配房间身份、玩法主机与房间内 roster。
/// </summary>
[MemoryPackable]
public partial class RoomAssignAction : NetAction
{
    public ushort RoomId { get; set; }
    public RoomMember[] Members { get; set; } = [];
}

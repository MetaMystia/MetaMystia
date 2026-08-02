using MemoryPack;

namespace MetaMystia.Network;

/// <summary>服务端端点 -> 进房者：分配房间身份与现有成员全量表。</summary>
[MemoryPackable]
public partial class RoomEnterAction : NetAction
{
    public PlayerFullData Self { get; set; }
    public PlayerFullData[] ExistingMembers { get; set; } = [];
}

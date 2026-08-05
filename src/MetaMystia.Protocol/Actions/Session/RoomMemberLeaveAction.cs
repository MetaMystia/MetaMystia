using MemoryPack;

namespace MetaMystia.Network;

/// <summary>服务端端点 -> 同房成员：某玩家离开房间。</summary>
[MemoryPackable]
public partial class RoomMemberLeaveAction : NetAction
{
    public int Uid { get; set; }
    public RoomLeaveReason Reason { get; set; }
}

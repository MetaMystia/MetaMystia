using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 所有客机：通告玩家离开
/// </summary>
[MemoryPackable]
public partial class PeerLeaveAction : NetAction
{
    public int PeerUid { get; set; }
    public RoomLeaveReason Reason { get; set; }
}

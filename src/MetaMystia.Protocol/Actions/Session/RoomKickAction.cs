using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class RoomKickAction : NetAction
{
    public int TargetUid { get; set; }
    public ushort RoomId { get; set; }
    public RoomKickReason Reason { get; set; }
}

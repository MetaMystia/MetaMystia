using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class RoomRequestRejectAction : NetAction
{
    public RoomRequestRejectReason Reason { get; set; }
}

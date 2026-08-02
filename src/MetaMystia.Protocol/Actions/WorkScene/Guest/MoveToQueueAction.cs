using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class MoveToQueueAction : NetAction
{
    public int RuntimeId { get; set; }
}

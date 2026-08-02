using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class SendFromQueueAction : NetAction
{
    public int RuntimeId { get; set; }
}

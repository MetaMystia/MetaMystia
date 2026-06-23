using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class PlayerRepellAction : NetAction
{
    public int RuntimeId { get; set; }
}

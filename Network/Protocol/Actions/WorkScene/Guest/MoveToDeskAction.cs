using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class MoveToDeskAction : NetAction
{
    public int RuntimeId { get; set; }
    public int DeskCode { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class GuestSpawnAction : NetAction
{
    public int RuntimeId { get; set; }
    public GuestSpawnInfo SpawnInfo { get; set; }
}

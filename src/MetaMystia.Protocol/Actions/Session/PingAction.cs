using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class PingAction : NetAction
{
    public int Id { get; set; }
}

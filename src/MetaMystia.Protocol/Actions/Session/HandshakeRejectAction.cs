using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class HandshakeRejectAction : NetAction
{
    public HandshakeRejectReason Reason { get; set; }
}

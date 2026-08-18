using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class ServerKickAction : NetAction
{
    public int TargetUid { get; set; }
    public ServerKickReason Reason { get; set; }
}

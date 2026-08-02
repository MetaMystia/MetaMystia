using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class GenerateOrderAction : NetAction
{
    public int RuntimeId { get; set; }
    public WireOrderGenerationResult Result { get; set; }
    public WireOrderGenerationResult? OverrideResult { get; set; }
    public WireOrderType OrderType { get; set; }
    public int RequestFood { get; set; }
    public int RequestBev { get; set; }
    public int DeskCode { get; set; }
    public bool NotShowInUI { get; set; }
    public bool FreeOrder { get; set; }
}

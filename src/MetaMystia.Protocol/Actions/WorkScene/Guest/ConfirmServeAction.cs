using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class ConfirmServeAction : NetAction
{
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFoodData Food { get; set; }
    public SellableFoodData Beverage { get; set; }
}

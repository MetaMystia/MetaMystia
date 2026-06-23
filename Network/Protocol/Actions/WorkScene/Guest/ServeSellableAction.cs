using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 上菜/撤回 Action。设计参考 docs/GuestFSM-Model.md §2.8。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class ServeSellableAction : NetAction
{
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFoodData Requested { get; set; }
    public SellableFoodData BasedOn { get; set; }
    public WireSellableType SellableType { get; set; }
}

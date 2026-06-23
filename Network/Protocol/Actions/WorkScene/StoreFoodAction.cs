using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个料理被放入保温箱中，与 ExtractFood 对应。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class StoreFoodAction : NetAction
{
    public SellableFoodData Food { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个料理被从保温箱中取出，与 StoreFood 对应。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class ExtractFoodAction : NetAction
{
    public SellableFoodData Food { get; set; }
}

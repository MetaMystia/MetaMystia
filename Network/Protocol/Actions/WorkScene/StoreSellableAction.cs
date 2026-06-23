using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家将 Sellable 储存在空厨具上。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class StoreSellableAction : NetAction
{
    public enum StoreType
    {
        Food,
        Beverage
    }

    public int GridIndex { get; set; }
    public SellableFoodData Food { get; set; }
    public int BeverageId { get; set; }
    public StoreType FoodType { get; set; }
}

using MemoryPack;

using GameData.Core.Collections;

using MetaMystia.Patch;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家将 Sellable 储存在空厨具上
/// </summary>
namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class StoreSellableAction : Action
{

    public enum StoreType
    {
        Food,
        Beverage
    }

    public int GridIndex { get; set; }
    public SellableFood Food { get; set; }
    public int BeverageId { get; set; }
    public StoreType FoodType { get; set; }

    protected override bool OnSendLogOnlyAction => true;
    protected override bool OnReceiveLogOnlyAction => true;

    public override void OnReceivedDerived()
    {
        Sellable sellable;
        switch (FoodType)
        {
            case StoreType.Food:
                sellable = Food.ToSellable();
                break;
            case StoreType.Beverage:
                sellable = BeverageId.RefBeverage();
                break;
            default:
                Log.LogError($"StoreSellableAction.OnReceived called with unsupported FoodType: {FoodType}");
                return;
        }
        var cookerController = CookManager.GetCookerControllerByIndex(GridIndex);
        if (cookerController == null)
        {
            Log.LogWarning($"Failed to find CookerController with GridIndex={GridIndex}");
            return;
        }
        CookControllerPatch.Store_ReversePatch(cookerController, sellable);
    }

    public static void Send(int gridIndex, Sellable sellable)
    {
        switch (sellable.type)
        {
            case Sellable.SellableType.Food:
                SellableFood food = SellableFood.FromSellable(sellable);
                var action = new StoreSellableAction
                {
                    GridIndex = gridIndex,
                    Food = food,
                    FoodType = StoreType.Food
                };
                action.Enqueue();
                break;
            case Sellable.SellableType.Beverage:
                int beverageId = sellable.id;
                action = new StoreSellableAction
                {
                    GridIndex = gridIndex,
                    BeverageId = beverageId,
                    FoodType = StoreType.Beverage
                };
                action.Enqueue();
                break;
            default:
                Log.LogError($"StoreSellableAction.Send called with unsupported sellable type: {sellable.type}");
                return;
        }
    }
}

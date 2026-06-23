using GameData.Core.Collections;
using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class StoreSellableBehavior
{
    public static void Send(int gridIndex, Sellable sellable)
    {
        switch (sellable.type)
        {
            case Sellable.SellableType.Food:
                var food = SellableFood.FromSellable(sellable);
                new StoreSellableAction
                {
                    GridIndex = gridIndex,
                    Food = food,
                    FoodType = StoreSellableAction.StoreType.Food
                }.Enqueue();
                break;
            case Sellable.SellableType.Beverage:
                new StoreSellableAction
                {
                    GridIndex = gridIndex,
                    BeverageId = sellable.id,
                    FoodType = StoreSellableAction.StoreType.Beverage
                }.Enqueue();
                break;
            default:
                Plugin.Instance?.Log.LogError($"StoreSellableBehavior.Send called with unsupported sellable type: {sellable.type}");
                return;
        }
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<StoreSellableAction>(Handle);
    }

    private static void Handle(StoreSellableAction action)
    {
        Sellable sellable;
        switch (action.FoodType)
        {
            case StoreSellableAction.StoreType.Food:
                sellable = action.Food.ToSellable();
                break;
            case StoreSellableAction.StoreType.Beverage:
                sellable = action.BeverageId.RefBeverage();
                break;
            default:
                Plugin.Instance?.Log.LogError($"StoreSellableAction.OnReceived called with unsupported FoodType: {action.FoodType}");
                return;
        }

        var cookerController = CookManager.GetCookerControllerByIndex(action.GridIndex);
        if (cookerController == null)
        {
            Plugin.Instance?.Log.LogWarning($"Failed to find CookerController with GridIndex={action.GridIndex}");
            return;
        }

        CookControllerPatch.Store_ReversePatch(cookerController, sellable);
    }
}

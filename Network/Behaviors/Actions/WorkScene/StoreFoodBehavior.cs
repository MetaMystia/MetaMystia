using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class StoreFoodBehavior
{
    public static void Send(SellableFoodData food) =>
        new StoreFoodAction { Food = food }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<StoreFoodAction>(Handle,
            scene: Common.UI.Scene.WorkScene);
    }

    private static void Handle(StoreFoodAction action)
    {
        IzakayaConfigurePatch.StoreFood_Original(action.Food.ToSellable());
        WorkSceneStoragePannelPatch.instanceRef?.UpdateFoodField();
        WorkSceneStoragePannelPatch.instanceRef?.m_FoodsGroup?.UpdateElements();
    }
}

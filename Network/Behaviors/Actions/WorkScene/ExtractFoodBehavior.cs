using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ExtractFoodBehavior
{
    public static void Send(SellableFoodData food) =>
        new ExtractFoodAction { Food = food }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ExtractFoodAction>(Handle,
            scene: Common.UI.Scene.WorkScene);
    }

    private static void Handle(ExtractFoodAction action)
    {
        GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance?.RemoveStoredFood(action.Food.GetFromLocal());
        WorkSceneStoragePannelPatch.instanceRef?.UpdateFoodField();
        WorkSceneStoragePannelPatch.instanceRef?.m_FoodsGroup?.UpdateElements();
    }
}

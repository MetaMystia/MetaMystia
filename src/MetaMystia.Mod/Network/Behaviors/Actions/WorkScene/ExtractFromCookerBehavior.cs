using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ExtractFromCookerBehavior
{
    public static void Send(int gridIndex) =>
        new ExtractFromCookerAction { GridIndex = gridIndex }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ExtractFromCookerAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(ExtractFromCookerAction action)
    {
        var cookerController = CookManager.GetCookerControllerByIndex(action.GridIndex);
        if (cookerController == null)
        {
            Plugin.Instance?.Log.LogWarning($"Failed to find CookerController with GridIndex={action.GridIndex}");
            return;
        }
        CookControllerPatch.Extract_ReversePatch(cookerController, null);
    }
}

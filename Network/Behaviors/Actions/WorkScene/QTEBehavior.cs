using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class QTEBehavior
{
    public static void Send(int gridIndex, float qteScore) =>
        new QTEAction { GridIndex = gridIndex, QTEScore = qteScore }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<QTEAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(QTEAction action)
    {
        var cookerController = CookManager.GetCookerControllerByIndex(action.GridIndex);
        if (cookerController == null)
        {
            Plugin.Instance?.Log.LogWarning($"Failed to find CookerController with GridIndex={action.GridIndex}");
            return;
        }
        CookControllerPatch.StartCookCountDown_ReversePatch(cookerController, action.QTEScore, false);
    }
}

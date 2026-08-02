namespace MetaMystia.Network;

internal static class ModNetActionBehaviors
{
    private static readonly NetActionDispatcher Dispatcher = CreateDispatcher();

    public static bool Dispatch(NetAction action) => Dispatcher.Dispatch(action);

    public static bool ShouldDiscardOnStory(NetAction action) => Dispatcher.ShouldDiscardOnStory(action);

    private static NetActionDispatcher CreateDispatcher()
    {
        var dispatcher = new NetActionDispatcher();
        NetActionBehaviorRegistry.RegisterAll(dispatcher);
        return dispatcher;
    }
}

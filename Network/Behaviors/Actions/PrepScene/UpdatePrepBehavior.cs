namespace MetaMystia.Network;

[NetActionBehavior]
internal static class UpdatePrepBehavior
{
    public static void Send(UpdatePrepAction.Table prepTable) =>
        new UpdatePrepAction { PrepTable = prepTable }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<UpdatePrepAction>(Handle,
            scene: Common.UI.Scene.IzakayaPrepScene);
    }

    private static void Handle(UpdatePrepAction action)
    {
        PrepSceneManager.MergeFromPeer(action.PrepTable);
    }
}

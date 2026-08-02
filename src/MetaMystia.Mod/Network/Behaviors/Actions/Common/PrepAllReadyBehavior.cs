using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PrepAllReadyBehavior
{
    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;
        new PrepAllReadyAction { PrepTable = PrepSceneManager.GetLocalPrepTableSnapshot() }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PrepAllReadyAction>(Handle,
            scene: Common.UI.Scene.IzakayaPrepScene,
            requireHostSender: true);
    }

    private static void Handle(PrepAllReadyAction action)
    {
        PrepSceneManager.ApplyHostTable(action.PrepTable);
        IzakayaConfigPannelPatch.PrepOver();
    }
}

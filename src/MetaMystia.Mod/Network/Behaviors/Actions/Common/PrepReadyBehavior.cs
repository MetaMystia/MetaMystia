using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PrepReadyBehavior
{
    public static void Send() => new PrepReadyAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PrepReadyAction>(Handle,
            scene: Common.UI.Scene.IzakayaPrepScene);
    }

    private static void Handle(PrepReadyAction action)
    {
        PlayerManager.SetPeerPrepOver(action.SenderUid);
        MpManager.PrepOver();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(action.SenderUid)));
    }
}

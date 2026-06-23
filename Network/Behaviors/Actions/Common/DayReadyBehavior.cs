using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class DayReadyBehavior
{
    public static void Send() => new DayReadyAction().Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<DayReadyAction>(Handle,
            scene: Common.UI.Scene.DayScene);
    }

    private static void Handle(DayReadyAction action)
    {
        PlayerManager.SetPeerDayOver(action.SenderUid);
        MpManager.DayOver();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(action.SenderUid)));
    }
}

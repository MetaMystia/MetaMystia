using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class DayAllReadyBehavior
{
    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;
        new DayAllReadyAction().Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<DayAllReadyAction>(Handle,
            scene: Common.UI.Scene.DayScene,
            requireHostSender: true);
    }

    private static void Handle(DayAllReadyAction action)
    {
        InGameConsole.ShowPassive(TextId.AllReadyTransition.Get());
        DaySceneManagerPatch.OnDayOver();
    }
}

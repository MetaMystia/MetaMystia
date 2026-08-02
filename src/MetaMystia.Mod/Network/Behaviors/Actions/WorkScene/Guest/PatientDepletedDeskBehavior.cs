namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PatientDepletedDeskBehavior
{
    public static void Send(int runtimeId) =>
        new PatientDepletedDeskAction { RuntimeId = runtimeId }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PatientDepletedDeskAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(PatientDepletedDeskAction action)
    {
        var rid = action.RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoPatientDepletedAtDesk),
            () => GuestFSM.DoPatientDepletedAtDesk(rid));
    }
}

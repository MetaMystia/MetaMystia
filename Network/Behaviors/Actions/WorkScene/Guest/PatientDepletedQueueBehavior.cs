namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PatientDepletedQueueBehavior
{
    public static void Send(int runtimeId) =>
        new PatientDepletedQueueAction { RuntimeId = runtimeId }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PatientDepletedQueueAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(PatientDepletedQueueAction action)
    {
        var rid = action.RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoPatientDepletedInQueue),
            () => GuestFSM.DoPatientDepletedInQueue(rid));
    }
}

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class MoveToQueueBehavior
{
    public static void Send(int runtimeId) =>
        new MoveToQueueAction { RuntimeId = runtimeId }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<MoveToQueueAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(MoveToQueueAction action)
    {
        var rid = action.RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoMoveToQueue),
            () => GuestFSM.DoMoveToQueue(rid));
    }
}

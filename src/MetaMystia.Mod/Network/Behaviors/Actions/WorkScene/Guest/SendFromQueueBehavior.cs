namespace MetaMystia.Network;

[NetActionBehavior]
internal static class SendFromQueueBehavior
{
    public static void Send(int runtimeId) =>
        new SendFromQueueAction { RuntimeId = runtimeId }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<SendFromQueueAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(SendFromQueueAction action)
    {
        var rid = action.RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoSendFromQueue),
            () => GuestFSM.DoSendFromQueue(rid));
    }
}

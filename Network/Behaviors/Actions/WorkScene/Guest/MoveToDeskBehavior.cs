namespace MetaMystia.Network;

[NetActionBehavior]
internal static class MoveToDeskBehavior
{
    public static void Send(int runtimeId, int deskCode) =>
        new MoveToDeskAction { RuntimeId = runtimeId, DeskCode = deskCode }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<MoveToDeskAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(MoveToDeskAction action)
    {
        var rid = action.RuntimeId;
        var deskCode = action.DeskCode;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoMoveToDesk),
            () => GuestFSM.DoMoveToDesk(rid, deskCode));
    }
}

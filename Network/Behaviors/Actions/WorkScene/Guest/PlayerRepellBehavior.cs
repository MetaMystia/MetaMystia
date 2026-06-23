namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PlayerRepellBehavior
{
    public static void Send(int runtimeId) =>
        new PlayerRepellAction { RuntimeId = runtimeId }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PlayerRepellAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(PlayerRepellAction action)
    {
        var rid = action.RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoPlayerRepell),
            () => GuestFSM.DoPlayerRepell(rid));
    }
}

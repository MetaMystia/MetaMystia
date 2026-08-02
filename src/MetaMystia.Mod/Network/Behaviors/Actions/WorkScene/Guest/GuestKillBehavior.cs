namespace MetaMystia.Network;

[NetActionBehavior]
internal static class GuestKillBehavior
{
    public static void Send(int runtimeId, GuestFSM.State hostStateBeforeKill, int deskCode) =>
        new GuestKillAction
        {
            RuntimeId = runtimeId,
            HostStateBeforeKill = (int)hostStateBeforeKill,
            DeskCode = deskCode
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<GuestKillAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(GuestKillAction action)
    {
        var rid = action.RuntimeId;
        var deskCode = action.DeskCode;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null)
        {
            GuestService.CleanGuestOrderRegistrationForDesk(deskCode);
            return;
        }

        Plugin.Instance?.Log.LogError(
            $"Guest #{action.RuntimeId} is being killed by host (host was {(GuestFSM.State)action.HostStateBeforeKill}, client was {fsm.CurrentState})");
        fsm.Kill();
    }
}

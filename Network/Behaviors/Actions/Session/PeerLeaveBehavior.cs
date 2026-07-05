using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PeerLeaveBehavior
{
    public static void Send(int leavingUid)
    {
        if (!MpManager.IsServerEndpoint) return;
        new PeerLeaveAction
        {
            SenderUid = MpConstants.HostUid,
            PeerUid = leavingUid,
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PeerLeaveAction>(Handle);
    }

    private static void Handle(PeerLeaveAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        if (!PlayerManager.PlayerTable.ContainsKey(action.PeerUid))
            return;

        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(action.PeerUid)));
        PlayerManager.RemovePeer(action.PeerUid);
    }
}

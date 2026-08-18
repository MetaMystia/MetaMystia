using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PeerLeaveBehavior
{
    public static void Send(int leavingUid, RoomLeaveReason reason)
    {
        if (!MpManager.IsServerEndpoint) return;
        new PeerLeaveAction
        {
            SenderUid = MpConstants.HostUid,
            PeerUid = leavingUid,
            Reason = reason,
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PeerLeaveAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(PeerLeaveAction action)
    {
        if (!MpManager.IsDirectClient || action.SenderUid != MpConstants.HostUid)
            return;

        if (!PlayerManager.PlayerTable.ContainsKey(action.PeerUid))
            return;

        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(action.PeerUid)));
        PlayerManager.RemovePeer(action.PeerUid);
    }
}

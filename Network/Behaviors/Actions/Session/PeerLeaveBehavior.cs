using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PeerLeaveBehavior
{
    public static void Send(int leavingUid)
    {
        if (!MpManager.IsRoomHost) return;
        new PeerLeaveAction { PeerUid = leavingUid }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PeerLeaveAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(PeerLeaveAction action)
    {
        if (!PlayerManager.TryGetRecord(action.PeerUid, out _))
            return;

        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerLeft.Get(LiveModeManager.GetDisplayName(action.PeerUid)));
        PlayerManager.RemovePeer(action.PeerUid);
    }
}

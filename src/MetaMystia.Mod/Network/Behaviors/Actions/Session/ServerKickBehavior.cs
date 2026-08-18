using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ServerKickBehavior
{
    public static void Send(int targetUid, ServerKickReason reason) =>
        new ServerKickAction
        {
            SenderUid = MpConstants.HostUid,
            TargetUid = targetUid,
            Reason = reason,
            WireTargetUid = targetUid,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ServerKickAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(ServerKickAction action)
    {
        if (action.SenderUid != MpConstants.HostUid || action.TargetUid != PlayerManager.Local.Uid)
            return;
        InGameConsole.ShowPassiveFromAnyThread(TextId.KickedFromServer.Get());
        MpWire.DisconnectPeer();
    }
}

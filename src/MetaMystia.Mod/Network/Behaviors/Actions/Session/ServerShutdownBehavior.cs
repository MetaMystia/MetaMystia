using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ServerShutdownBehavior
{
    public static void Broadcast() =>
        new ServerShutdownAction
        {
            SenderUid = MpConstants.HostUid,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ServerShutdownAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(ServerShutdownAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;
        InGameConsole.ShowPassiveFromAnyThread(TextId.ServerClosed.Get());
        MpWire.DisconnectPeer();
    }
}

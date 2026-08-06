using Common.UI;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomNewPlayerJoinedBehavior
{
    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RoomNewPlayerJoinedAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(RoomNewPlayerJoinedAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        var joined = action.Joined;
        if (joined == null || joined.Uid == PlayerManager.Local.Uid)
            return;

        var peer = PlayerManager.UpsertFullPlayer(joined);
        if (MpManager.LocalScene == Scene.DayScene && peer != null)
            PlayerManager.SpawnPeersForCurrentScene(new[] { peer });
    }
}

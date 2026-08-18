using System.Linq;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomAssignBehavior
{
    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RoomAssignAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(RoomAssignAction action)
    {
        if (action.SenderUid != MpConstants.HostUid)
            return;

        var self = action.Self;
        if (self == null)
        {
            Plugin.Instance?.Log.LogError("RoomAssignAction has no self player");
            MpWire.DisconnectPeer();
            return;
        }

        var existing = action.ExistingMembers ?? [];
        var host = existing.FirstOrDefault(p => p.Role == WireRoomRole.Host)
            ?? (self.Role == WireRoomRole.Host ? self : null);
        if (host == null)
        {
            Plugin.Instance?.Log.LogError("RoomAssignAction has no room host");
            MpWire.DisconnectPeer();
            return;
        }

        MpManager.EndRoomRequest();
        bool wasInRoom = MpManager.IsInRoom;

        if (MpWire.Session.IsRelay)
            MpWire.Session.EnterRelayRoom(host.Uid);
        else
        {
            MpWire.Session.EnterDirectClientRoom();
            MpWire.Session.AssignHostUid(MpConstants.HostUid);
        }

        PlayerManager.Local.RoomId = self.RoomId;
        PlayerManager.Local.Role = self.Role;

        foreach (var member in existing)
            PlayerManager.UpsertFullPlayer(member);

        if (MpManager.LocalScene == Common.UI.Scene.DayScene)
            PlayerManager.SpawnPeersForCurrentScene(PlayerManager.RoomPeers);

        if (!wasInRoom)
        {
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.MpEnteredRoom.Get(MpSession.FormatRoomId(self.RoomId)));
            MpWire.OnHandshakeComplete(host.PeerId);
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.MpConnected.Get(LiveModeManager.GetDisplayName(host.Uid, host.PeerId)));
        }
    }
}

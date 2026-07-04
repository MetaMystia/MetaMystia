using System.Linq;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomAssignBehavior
{
    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;

        new RoomAssignAction
        {
            Players = PlayerManager.RoomPlayers.Select(player => player.ToFullData()).ToArray(),
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        // relay 模式下 server 也会给房主发 RoomAssign 以更新 roster；direct 模式下 host 不会收到此动作。
        dispatcher.Register<RoomAssignAction>(Handle,
            receiveScope: NetReceiveScope.Any);
    }

    private static void Handle(RoomAssignAction action)
    {
        bool wasInRoom = MpManager.IsInRoom;
        var players = action.Players ?? [];
        var self = System.Linq.Enumerable.FirstOrDefault(players, p => p.Uid == PlayerManager.Local.Uid);
        var host = System.Linq.Enumerable.FirstOrDefault(players, p => p.Role == WireRoomRole.Host);

        if (self == null || host == null)
        {
            RejectBehavior.ShowAndDisconnect(RejectReason.Unknown);
            return;
        }

        if (MpWire.Session.TransportKind == TransportKind.RelayClient)
            MpWire.Session.EnterRelayRoom(host.Uid);
        else
            MpWire.Session.EnterDirectClientRoom();

        MpWire.Session.AssignHostUid(host.Uid);
        PlayerManager.SyncRoomPeersBeforeAssign(self.RoomId, System.Linq.Enumerable.Select(players, p => p.Uid));
        PlayerManager.Local.RoomId = self.RoomId;
        PlayerManager.Local.Role = self.Role;

        foreach (var player in players)
            PlayerManager.UpsertFullPlayer(player);

        // 仅在首次进入房间时提示；roster 刷新（加入/离开触发）不重复显示。
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

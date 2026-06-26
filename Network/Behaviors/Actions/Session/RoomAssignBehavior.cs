using Common.UI;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RoomAssignBehavior
{
    public static void SendDirect(int clientUid)
    {
        if (!MpManager.IsRoomHost) return;

        var members = BuildDirectMembers();

        new RoomAssignAction
        {
            RoomId = MpSession.DirectRoomId,
            Members = members,
            WireTargetUid = clientUid,
        }.Enqueue();
    }

    public static void BroadcastDirectExcept(int exceptUid)
    {
        if (!MpManager.IsRoomHost) return;
        new RoomAssignAction
        {
            RoomId = MpSession.DirectRoomId,
            Members = BuildDirectMembers(),
            WireExceptUid = exceptUid,
        }.Enqueue();
    }

    private static RoomMember[] BuildDirectMembers()
    {
        var members = new System.Collections.Generic.List<RoomMember>
        {
            PlayerManager.RoomMemberFromPeer(PlayerManager.Local, WireRoomRole.Host, MpManager.LocalScene.ToWire())
        };
        foreach (var peer in PlayerManager.Peers.Values)
        {
            var scene = PlayerManager.TryGetRecord(peer.Uid, out var record)
                ? record.Scene
                : MpManager.LocalScene.ToWire();
            members.Add(PlayerManager.RoomMemberFromPeer(peer, WireRoomRole.Client, scene));
        }
        return members.ToArray();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        // relay 模式下 server 也会给房主发 RoomAssign 以更新 roster；direct 模式下 host 不会收到此动作。
        dispatcher.Register<RoomAssignAction>(Handle,
            receiveScope: NetReceiveScope.Any);
    }

    private static void Handle(RoomAssignAction action)
    {
        bool wasInRoom = MpWire.Session.IsInRoom;
        var members = action.Members ?? [];
        var self = System.Linq.Enumerable.FirstOrDefault(members, m => m.Uid == PlayerManager.Local.Uid);
        var host = System.Linq.Enumerable.FirstOrDefault(members, m => m.Role == WireRoomRole.Host);

        if (self == null || host == null)
        {
            RejectBehavior.ShowAndDisconnect(RejectReason.Unknown);
            return;
        }

        var role = self.Role switch
        {
            WireRoomRole.Host => RoomRole.Host,
            WireRoomRole.Client => RoomRole.Client,
            _ => RoomRole.None,
        };

        if (MpWire.Session.TransportKind == TransportKind.RelayClient)
            MpWire.Session.EnterRelayRoom(role, action.RoomId, host.Uid);
        else
            MpWire.Session.EnterDirectClientRoom();

        MpWire.Session.AssignHostUid(host.Uid);
        PlayerManager.SyncRoomPeersBeforeAssign(action.RoomId, System.Linq.Enumerable.Select(members, m => m.Uid));

        foreach (var member in members)
        {
            PlayerManager.UpsertRoomMember(member, action.RoomId);
            if (PlayerManager.TryGetRoomPeer(member.Uid, out var peer))
            {
                peer.IsDayOver = member.IsDayOver;
                peer.IsPrepOver = member.IsPrepOver;
            }
        }

        // 仅在首次进入房间时提示；roster 刷新（加入/离开触发）不重复显示。
        if (!wasInRoom)
        {
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.MpEnteredRoom.Get(MpSession.FormatRoomId(action.RoomId)));
            MpWire.OnHandshakeComplete(host.PeerId);
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.MpConnected.Get(LiveModeManager.GetDisplayName(host.Uid, host.PeerId)));
        }
    }
}

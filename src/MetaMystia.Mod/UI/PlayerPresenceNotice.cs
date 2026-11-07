using System.Linq;

using MetaMystia.Network;

namespace MetaMystia.UI;

/// <summary>根据前后成员列表显示进出提示，不把首次同步和资料更新当作加入。</summary>
public static class PlayerPresenceNotice
{
    public static void ShowChanges(Snapshot previous, Snapshot current, int localUid)
    {
        if (previous.World.Length == 0) return;

        var oldWorld = previous.World.Select(p => p.Uid).ToHashSet();
        var newWorld = current.World.Select(p => p.Uid).ToHashSet();
        foreach (var player in current.World.Where(p => p.Uid != localUid && !oldWorld.Contains(p.Uid)))
            Show(TextId.PeerConnected, player);

        if (previous.Room is { } oldRoom)
        {
            var oldMembership = oldRoom.Members.FirstOrDefault(p => p.Uid == localUid)?.Membership;
            var newMembership = current.Room?.Members.FirstOrDefault(p => p.Uid == localUid)?.Membership;
            if (current.Room?.Id == oldRoom.Id && oldMembership == newMembership)
            {
                foreach (var player in oldRoom.Members.Where(p => p.Uid != localUid
                    && !current.Room.Members.Any(n => n.Uid == p.Uid && n.Membership == p.Membership)))
                    Show(TextId.PeerLeft, player);
                foreach (var player in current.Room.Members.Where(p => p.Uid != localUid
                    && !oldRoom.Members.Any(n => n.Uid == p.Uid && n.Membership == p.Membership)))
                    Show(TextId.PeerJoined, player);
            }
            else
            {
                // 房间解散时原成员都已退房；本人主动离开仍存在的房间时不误报其他人。
                if (!current.Rooms.Any(r => r.Id == oldRoom.Id))
                    foreach (var player in oldRoom.Members.Where(p => p.Uid != localUid))
                        Show(TextId.PeerLeft, player);
                InGameConsole.ShowPassive(TextId.NetworkRoomLeft.Get(RoomCode.Format(oldRoom.Id)));
            }
        }

        foreach (var player in previous.World.Where(p => p.Uid != localUid && !newWorld.Contains(p.Uid)))
            Show(TextId.PeerDisconnected, player);
    }

    private static void Show(TextId text, Player player)
    {
        var name = LiveModeManager.GetDisplayName(player.Uid, player.Name)
            .Replace("<", "＜").Replace(">", "＞");
        InGameConsole.ShowPassive(text.Get(name));
    }
}

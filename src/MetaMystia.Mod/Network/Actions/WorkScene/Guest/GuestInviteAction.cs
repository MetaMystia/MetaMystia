using MemoryPack;
using System.Collections.Generic;
using System.Linq;

using GameData.RunTime.Common;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 客机 -> 主机：同步客机白天邀请的稀客列表，主机在夜晚前合并到自己的邀请列表。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestInviteAction : Action
{

    public List<int> InvitedGuestIds { get; set; } = [];

    [HostOnlyReceive]
    public override void OnReceivedDerived()
    {
        var invitedGuestIds = InvitedGuestIds ?? [];
        var tracker = StatusTracker.Instance;
        if (tracker == null) return;

        foreach (var guestId in invitedGuestIds.Distinct().Where(PlayerManager.SpecialGuestAvailable))
        {
            StatusTrackerPatch.RecordInvitedGuest_ReversePatch(tracker, guestId);
        }
    }

    public static void Send(List<int> invitedGuestIds)
    {
        if (!MpManager.IsRoomClient) return;
        new GuestInviteAction { InvitedGuestIds = invitedGuestIds ?? [] }.Enqueue();
    }
}

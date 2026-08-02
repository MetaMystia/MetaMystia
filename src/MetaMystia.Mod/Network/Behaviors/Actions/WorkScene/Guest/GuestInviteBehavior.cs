using System.Collections.Generic;
using System.Linq;

using GameData.RunTime.Common;
using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class GuestInviteBehavior
{
    public static void Send(List<int> invitedGuestIds)
    {
        if (!MpManager.IsRoomClient) return;
        new GuestInviteAction { InvitedGuestIds = invitedGuestIds ?? [] }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<GuestInviteAction>(Handle,
            receiveScope: NetReceiveScope.HostOnly);
    }

    private static void Handle(GuestInviteAction action)
    {
        var invitedGuestIds = action.InvitedGuestIds ?? [];
        var tracker = StatusTracker.Instance;
        if (tracker == null) return;

        foreach (var guestId in invitedGuestIds.Distinct().Where(PlayerManager.SpecialGuestAvailable))
            StatusTrackerPatch.RecordInvitedGuest_ReversePatch(tracker, guestId);
    }
}

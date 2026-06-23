using System.Collections.Generic;
using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 客机 -> 主机：同步客机白天邀请的稀客列表，主机在夜晚前合并到自己的邀请列表。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class GuestInviteAction : NetAction
{
    public List<int> InvitedGuestIds { get; set; } = [];
}

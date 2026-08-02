using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机权威：顾客离桌主链 (FSM: * → Leaving → Left)。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class GuestLeaveAction : NetAction
{
    public int RuntimeId { get; set; }
    public WireLeaveType LeaveType { get; set; }
    public bool TriggerLeaveBuff { get; set; }
}

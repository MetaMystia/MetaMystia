using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机 FSM 异常 (FallBack) 时广播的强制清理信号。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class GuestKillAction : NetAction
{
    public int RuntimeId { get; set; }
    public int HostStateBeforeKill { get; set; }
    public int DeskCode { get; set; } = -1;
}

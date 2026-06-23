using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机判定桌上耐心耗尽。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class PatientDepletedDeskAction : NetAction
{
    public int RuntimeId { get; set; }
}

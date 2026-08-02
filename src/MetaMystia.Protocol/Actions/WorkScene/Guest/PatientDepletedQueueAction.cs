using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机判定排队耐心耗尽。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class PatientDepletedQueueAction : NetAction
{
    public int RuntimeId { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>任何玩家 → 所有玩家：通告本人白天阶段就绪（DayScene）。</summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class DayReadyAction : NetAction
{
}

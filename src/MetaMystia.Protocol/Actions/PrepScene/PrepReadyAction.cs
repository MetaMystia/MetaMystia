using MemoryPack;

namespace MetaMystia.Network;

/// <summary>任何玩家 → 所有玩家：通告本人备菜阶段就绪（IzakayaPrepScene）。</summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class PrepReadyAction : NetAction
{
}

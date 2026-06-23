using MemoryPack;

namespace MetaMystia.Network;

/// <summary>主机 → 全体玩家：确认白天阶段全员就绪，客机收到后推进场景。</summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class DayAllReadyAction : NetAction
{
}

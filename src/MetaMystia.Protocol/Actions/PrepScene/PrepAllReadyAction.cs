using MemoryPack;

namespace MetaMystia.Network;

/// <summary>主机 → 全体玩家：确认备菜阶段全员就绪，并下发主机权威备菜表。</summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class PrepAllReadyAction : NetAction
{
    public UpdatePrepAction.Table PrepTable { get; set; } = new();
}

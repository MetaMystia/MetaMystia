using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家 ID 变更
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class PlayerChangeIdAction : NetAction
{
    public string NewPlayerId { get; set; }
}

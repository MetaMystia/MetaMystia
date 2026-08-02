using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 所有玩家 → 所有玩家：通告自身 Scene 切换。
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class SceneTransitAction : NetAction
{
    public WireScene Scene { get; set; }
}

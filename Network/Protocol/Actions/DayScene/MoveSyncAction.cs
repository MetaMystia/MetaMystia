using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告角色移动同步，主要是白天。
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class MoveSyncAction : NetAction
{
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Px { get; set; }
    public float Py { get; set; }
    public bool IsSprinting { get; set; }
    public float Speed { get; set; }
    public MapLabel MapLabel { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：白天角色移动同步（公域可见）。
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class DayMoveSyncAction : NetAction
{
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Px { get; set; }
    public float Py { get; set; }
    public bool IsSprinting { get; set; }
    public float Speed { get; set; }
    public MapLabel MapLabel { get; set; }
}

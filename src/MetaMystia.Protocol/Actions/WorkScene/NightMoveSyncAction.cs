using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：夜间角色移动同步。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class NightMoveSyncAction : NetAction
{
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Px { get; set; }
    public float Py { get; set; }
    public float Speed { get; set; }
}

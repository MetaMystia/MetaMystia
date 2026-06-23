using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家所选店铺地点和等级。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class SelectIzakayaAction : NetAction
{
    public MapLabel MapLabel { get; set; }
    public int MapLevel { get; set; } = 0;
}

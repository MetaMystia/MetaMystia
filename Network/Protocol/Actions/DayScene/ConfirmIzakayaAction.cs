using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 全体客机：确认全员选店一致，客机收到后执行场景切换。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class ConfirmIzakayaAction : NetAction
{
    public MapLabel MapLabel { get; set; }
    public int MapLevel { get; set; } = 0;
}

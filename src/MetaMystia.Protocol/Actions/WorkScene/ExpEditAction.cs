using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.ExpEdit 的网络同步。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class ExpEditAction : NetAction
{
    public float Value { get; set; }
    public WireMathOperation MathOp { get; set; }
}

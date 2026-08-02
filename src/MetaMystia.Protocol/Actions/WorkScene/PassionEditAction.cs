using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.PassionEdit 的网络同步。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class PassionEditAction : NetAction
{
    public float Value { get; set; }
    public WireMathOperation MathOp { get; set; }
}

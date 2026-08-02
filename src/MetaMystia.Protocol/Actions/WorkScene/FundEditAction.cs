using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.FundEdit 的网络同步。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class FundEditAction : NetAction
{
    public float Value { get; set; }
    public WireMathOperation MathOp { get; set; }
}

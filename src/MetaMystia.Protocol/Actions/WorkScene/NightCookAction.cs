using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告锁定某个厨具以准备烹饪某个料理，总是在 QTEAction 之前触发。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class NightCookAction : NetAction
{
    public int GridIndex { get; set; }
    public int RecipeId { get; set; }
    public SellableFoodData Food { get; set; }
}

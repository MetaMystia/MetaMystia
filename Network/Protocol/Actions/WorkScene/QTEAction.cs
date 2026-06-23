using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个厨具的 QTE 结果以启动料理倒计时，总是在 CookAction 之后触发。
/// QTE(Quick Time Event): 夜雀之歌。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class QTEAction : NetAction
{
    public int GridIndex { get; set; }
    public float QTEScore { get; set; }
}

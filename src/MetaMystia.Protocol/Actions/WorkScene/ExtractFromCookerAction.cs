using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个厨具（包括空厨具）中的料理被取出。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class ExtractFromCookerAction : NetAction
{
    public int GridIndex { get; set; }
}

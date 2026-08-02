using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.TipEdit 的网络同步。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class TipEditAction : NetAction
{
    public int IntValue { get; set; }
    public WireServeType ServeType { get; set; }
    public float ComboBuff { get; set; }
    public float MoodBuff { get; set; }
    public float ExtraBuff { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告触发 QTE Buff。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class BuffAction : NetAction
{
    public QTEBuff Buff;
}

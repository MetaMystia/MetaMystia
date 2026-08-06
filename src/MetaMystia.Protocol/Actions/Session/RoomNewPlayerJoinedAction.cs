using MemoryPack;

namespace MetaMystia.Network;

/// <summary>服务端端点 -> 同房老成员：通告新玩家加入房间。</summary>
[MemoryPackable]
public partial class RoomNewPlayerJoinedAction : NetAction
{
    public PlayerFullData Joined { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>服务端端点 -> 同房老成员：新成员加入。</summary>
[MemoryPackable]
public partial class RoomMemberJoinAction : NetAction
{
    public PlayerFullData Joined { get; set; }
}

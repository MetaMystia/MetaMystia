using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任一玩家 -> 服务端端点：离开当前房间但保持 relay 连接。
/// </summary>
[MemoryPackable]
public partial class LeaveRoomAction : NetAction
{
}

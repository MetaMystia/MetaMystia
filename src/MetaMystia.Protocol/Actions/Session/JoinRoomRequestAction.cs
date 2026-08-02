using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 客机 -> 服务端端点：请求加入指定房间。直连下仅允许默认房间。
/// </summary>
[MemoryPackable]
public partial class JoinRoomRequestAction : NetAction
{
    public ushort RoomId { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任一玩家 -> 服务端端点：请求随机分配一个新房间并成为房主。
/// roomId 由服务端分配；客户端不指定。
/// </summary>
[MemoryPackable]
public partial class CreateRoomRequestAction : NetAction
{
}
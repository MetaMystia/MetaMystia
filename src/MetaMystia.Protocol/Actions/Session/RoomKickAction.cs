using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 服务端端点 -> 客机：软踢出房间，relay 连接保持在公域。
/// </summary>
[MemoryPackable]
public partial class RoomKickAction : NetAction
{
    public RejectReason Reason { get; set; }
    public string[] Args { get; set; } = [];
}

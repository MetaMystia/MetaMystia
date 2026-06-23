using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：发送聊天消息
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class MessageAction : NetAction
{
    public string Message { get; set; }
}

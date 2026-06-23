using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 客机：连接被拒绝，携带拒绝原因。客机收到后显示通知并断开。
/// </summary>
[MemoryPackable]
public partial class RejectAction : NetAction
{
    public RejectReason Reason { get; set; }
    public string[] Args { get; set; } = [];
}

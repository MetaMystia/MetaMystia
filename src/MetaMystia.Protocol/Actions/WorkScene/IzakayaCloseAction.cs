using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 所有客机：广播打烊。
/// </summary>
[MemoryPackable]
[NetAction.RoomRelay]
public partial class IzakayaCloseAction : NetAction
{
}

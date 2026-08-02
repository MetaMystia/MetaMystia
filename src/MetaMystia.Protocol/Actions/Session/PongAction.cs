using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class PongAction : NetAction
{
    public int Id { get; set; }

    /// <summary>
    /// 主机收到对应 Ping 那一刻的 NowMs。客机据此估算本地与主机的时钟偏移。
    /// </summary>
    public long HostReceivedMs { get; set; }
}

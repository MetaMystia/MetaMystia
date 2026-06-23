using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 客机 -> 服务端端点：入房握手前查询版本与拓扑模式。
/// </summary>
[MemoryPackable]
public partial class ServerInfoRequestAction : NetAction
{
    public string ClientGameVersion { get; set; } = "";
    public string ClientModVersion { get; set; } = "";
}

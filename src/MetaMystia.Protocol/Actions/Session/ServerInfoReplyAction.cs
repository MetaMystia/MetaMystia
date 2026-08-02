using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 服务端端点 -> 客机：入房握手前返回版本与拓扑模式。
/// </summary>
[MemoryPackable]
public partial class ServerInfoReplyAction : NetAction
{
    public string GameVersion { get; set; } = "";
    public string ModVersion { get; set; } = "";
    public ServerMode ServerMode { get; set; }
    public int MaxPlayers { get; set; }
    public int OnlineCount { get; set; }
}

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 客机 → 主机：握手请求。主机验证后回复 HelloAckAction。
/// </summary>
[MemoryPackable]
public partial class HelloAction : NetAction
{
    public string ModVersion { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public PlayerFullData Player { get; set; }
}

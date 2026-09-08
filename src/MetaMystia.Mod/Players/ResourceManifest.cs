using MemoryPack;

namespace MetaMystia;

[MemoryPackable]
public sealed partial class ResourceManifest
{
    // 每类都是完整替换；零长度表示收到有效空表，null 表示非法载荷。
    public int[][] Categories { get; set; } = new int[9][];
}

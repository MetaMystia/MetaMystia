using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 皮肤变更网络同步 Action。
/// 当玩家通过 /skin 命令更改皮肤时，广播给所有其他玩家。
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class PlayerChangeSkinAction : NetAction
{
    public PlayerSkinData Skin { get; set; }
}

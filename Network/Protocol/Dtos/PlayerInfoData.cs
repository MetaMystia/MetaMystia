using MemoryPack;

namespace MetaMystia.Network;

// 数据半边（协议层）：Hello 上行完整自报信息，零游戏依赖。
// 行为半边（FromPlayer 依赖 NetPlayer）见 Players/PlayerInfo.cs。

[MemoryPackable]
public partial class PlayerInfoData
{
    public int Uid { get; set; }
    public string PeerId { get; set; } = "";
    public ResourceDataBaseData IncrementalDataBase { get; set; }
    public PlayerSkinData Skin { get; set; }
    public bool IsDayOver { get; set; }
    public bool IsPrepOver { get; set; }
}

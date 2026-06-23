using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 公域层轻量玩家记录：不含资源表。
/// </summary>
[MemoryPackable]
public partial class PlayerSummary
{
    public int Uid { get; set; }
    public string PeerId { get; set; } = "";
    public ushort RoomId { get; set; } = MpConstants.PublicRoomId;
    public WireScene Scene { get; set; }
    public PlayerSkinData Skin { get; set; }
    public WireRoomRole Role { get; set; } = WireRoomRole.None;
}

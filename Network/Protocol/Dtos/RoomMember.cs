using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 房间层完整玩家记录：含资源表。
/// </summary>
[MemoryPackable]
public partial class RoomMember
{
    public int Uid { get; set; }
    public string PeerId { get; set; } = "";
    public WireRoomRole Role { get; set; } = WireRoomRole.None;
    public WireScene Scene { get; set; }
    public PlayerSkinData Skin { get; set; }
    public ResourceDataBaseData Resources { get; set; }
    public bool IsDayOver { get; set; }
    public bool IsPrepOver { get; set; }
}

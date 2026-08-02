using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// Lite player row for lightweight lobby/public roster updates.
/// </summary>
[MemoryPackable]
public partial class PlayerLiteData
{
    public int Uid { get; set; }
    public ushort RoomId { get; set; } = MpConstants.PublicRoomId;
    public WireRoomRole Role { get; set; } = WireRoomRole.None;
    public string PeerId { get; set; } = "";
    public WireScene Scene { get; set; }
    public PlayerSkinData Skin { get; set; }
}

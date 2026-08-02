using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// Full player row for Hello self-report and room roster snapshots.
/// </summary>
[MemoryPackable]
public partial class PlayerFullData
{
    public int Uid { get; set; }
    public ushort RoomId { get; set; } = MpConstants.PublicRoomId;
    public WireRoomRole Role { get; set; } = WireRoomRole.None;
    public string PeerId { get; set; } = "";
    public WireScene Scene { get; set; }
    public PlayerSkinData Skin { get; set; }
    public ResourceDataBaseData Resources { get; set; }
    public bool IsDayOver { get; set; }
    public bool IsPrepOver { get; set; }

    public PlayerLiteData ToLiteData() => new()
    {
        Uid = Uid,
        RoomId = RoomId,
        Role = Role,
        PeerId = PeerId ?? "",
        Scene = Scene,
        Skin = Skin,
    };
}

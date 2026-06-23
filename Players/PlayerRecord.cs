using MetaMystia.Network;

namespace MetaMystia;

public sealed class PlayerRecord
{
    public int Uid { get; set; }
    public string PeerId { get; set; } = "";
    public ushort RoomId { get; set; } = MpConstants.PublicRoomId;
    public WireScene Scene { get; set; }
    public PlayerSkinData Skin { get; set; } = new();
    public ResourceDataBaseData Resources { get; set; }
    public WireRoomRole Role { get; set; } = WireRoomRole.None;
    public PeerPlayer Player { get; set; }

    public bool HasResources => Resources != null;
}

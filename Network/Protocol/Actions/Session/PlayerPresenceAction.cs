using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 服务端端点 -> 所有人：公域层轻量玩家增量。
/// </summary>
[MemoryPackable]
[NetAction.PublicRelay]
public partial class PlayerPresenceAction : NetAction
{
    public int Uid { get; set; }
    public string PeerId { get; set; } = "";
    public ushort RoomId { get; set; } = MpConstants.PublicRoomId;
    public WireScene Scene { get; set; }
    public PlayerSkinData Skin { get; set; }
}

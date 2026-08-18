using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// HelloAck 中携带的已有 peer 信息
/// </summary>
[MemoryPackable]
public partial class PlayerInfo
{
    public int Uid { get; set; }
    public string PeerId { get; set; } = "";
    public ResourceDataBase IncrementalDataBase { get; set; }
    public PlayerSkin Skin { get; set; }
    public bool IsDayOver { get; set; }
    public bool IsPrepOver { get; set; }

    public static PlayerInfo FromPlayer(NetPlayer player)
    {
        return new PlayerInfo
        {
            Uid = player.Uid,
            PeerId = player.Id,
            IncrementalDataBase = player.IncrementalDataBase,
            Skin = player.Skin,
            IsDayOver = player.IsDayOver,
            IsPrepOver = player.IsPrepOver
        };
    }
}

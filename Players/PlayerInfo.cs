using MetaMystia;

namespace MetaMystia.Network;

// 行为半边（mod）：依赖 NetPlayer 的工厂方法。
// 数据半边（序列化字段）见 Network/Protocol/Dtos/PlayerInfoData.cs。

internal static class PlayerInfo
{
    public static PlayerInfoData FromPlayer(NetPlayer player)
    {
        return new PlayerInfoData
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

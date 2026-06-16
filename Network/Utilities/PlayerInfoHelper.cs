using MetaMystia.Protocol.Data;

// ReSharper disable once CheckNamespace
namespace MetaMystia;

public static class PlayerInfoHelper
{
    public static PlayerInfoData ToPlayerInfoData(this NetPlayer player)
    {
        return new PlayerInfoData
        {
            Uid = player.Uid,
            PeerId = player.Id,
            IncrementalDataBase = player.IncrementalDataBase?.ToDatabaseData() ?? new ResourceDatabaseData(),
            Skin = player.Skin,
            IsDayOver = player.IsDayOver,
            IsPrepOver = player.IsPrepOver
        };
    }
}

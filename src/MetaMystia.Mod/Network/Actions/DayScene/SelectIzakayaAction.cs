using MemoryPack;

using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家所选店铺地点和等级
/// </summary>
[MemoryPackable]
public partial class SelectIzakayaAction : Action
{
    public MapLabel MapLabel { get; set; }
    public int MapLevel { get; set; } = 0;
    public override void OnReceivedDerived()
    {
        RoomGameplay.ReceiveSelection(SenderUid, MapLabel, MapLevel);
    }

    public static void Send(MapLabel mapLabel, int level) =>
        new SelectIzakayaAction { MapLabel = mapLabel, MapLevel = level }.Enqueue();
}

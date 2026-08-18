using MemoryPack;

using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家所选店铺地点和等级
/// </summary>
[MemoryPackable]
[RoomRelay]
public partial class SelectIzakayaAction : Action
{
    public MapLabel MapLabel { get; set; }
    public int MapLevel { get; set; } = 0;
    public override void OnReceivedDerived()
    {
        PlayerManager.SetPeerIzakayaSelection(SenderUid, MapLabel, MapLevel);

        var peerName = LiveModeManager.GetDisplayName(SenderUid);
        InGameConsole.ShowPassive(TextId.PeerSelectedIzakaya.Get(
            $"{peerName}", MapLabel.FormatIzakayaSelection(MapLevel)));

        if (MpManager.IsServer)
        {
            // 主机收到 SELECT 后自动检查全员是否一致
            IzakayaSelectorPanelPatch.TryConfirmSelection();
        }
        else
        {
            // 客机也显示当前选店状态摘要
            IzakayaSelectorPanelPatch.ShowSelectionStatus();
        }
    }

    public static void Send(MapLabel mapLabel, int level) =>
        new SelectIzakayaAction { MapLabel = mapLabel, MapLevel = level }.Enqueue();
}

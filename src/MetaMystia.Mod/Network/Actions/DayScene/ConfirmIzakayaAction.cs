using Common.UI;
using MemoryPack;

using MetaMystia.Patch;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 全体客机：确认全员选店一致，客机收到后执行场景切换。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ConfirmIzakayaAction : Action
{
    public MapLabel MapLabel { get; set; }
    public int MapLevel { get; set; } = 0;

    public override void OnReceivedDerived()
    {
        var display = MapLabel.FormatIzakayaSelection(MapLevel);
        InGameConsole.ShowPassive(TextId.SelectedIzakaya.Get(display));

        IzakayaSelectorPanelPatch.TryProceedWithConfirmedSelection(MapLabel, (IzakayaLevel)MapLevel);
    }

    /// <summary>
    /// 主机广播确认选店
    /// </summary>
    public static void Send(MapLabel mapLabel, int mapLevel)
    {
        if (!MpManager.IsRoomHost) return;
        new ConfirmIzakayaAction { MapLabel = mapLabel, MapLevel = mapLevel }.Enqueue();
    }
}

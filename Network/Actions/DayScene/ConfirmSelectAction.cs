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
public partial class ConfirmSelectAction : Action
{
    public override ActionType Type => ActionType.CONFIRM_SELECT;
    public string MapLabel { get; set; } = "";
    public int MapLevel { get; set; } = 0;

    public override void OnReceivedDerived()
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var display = $"{MystiaUtils.GetMapLabelNameCN(MapLabel)} {MystiaUtils.GetMapLevelNameCN(MapLevel)}";
            InGameConsole.ShowPassive(TextId.SelectedIzakaya.Get(display));
            SgrYuki.Utils.Panel.CloseActivePanelsBeforeSceneTransit();

            if (IzakayaSelectorPanelPatch.instanceRef != null)
            {
                IzakayaSelectorPanelPatch._OnGuideMapInitialize_b__21_0_Original(
                    IzakayaSelectorPanelPatch.instanceRef);
            }
            else
            {
                Log.LogWarning("ConfirmSelectAction: instanceRef is null, cannot call original method");
            }
        });
    }

    /// <summary>
    /// 主机广播确认选店
    /// </summary>
    public static void Broadcast(string mapLabel, int mapLevel)
    {
        var action = new ConfirmSelectAction
        {
            MapLabel = mapLabel,
            MapLevel = mapLevel
        };
        action.SendToHostOrBroadcast();
    }
}

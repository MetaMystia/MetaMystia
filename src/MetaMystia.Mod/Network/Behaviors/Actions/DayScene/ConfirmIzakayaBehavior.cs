using Common.UI;
using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ConfirmIzakayaBehavior
{
    /// <summary>
    /// 主机广播确认选店。
    /// </summary>
    public static void Send(MapLabel mapLabel, int mapLevel)
    {
        if (!MpManager.IsRoomHost) return;
        new ConfirmIzakayaAction { MapLabel = mapLabel, MapLevel = mapLevel }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ConfirmIzakayaAction>(Handle);
    }

    private static void Handle(ConfirmIzakayaAction action)
    {
        var display = action.MapLabel.FormatIzakayaSelection(action.MapLevel);
        InGameConsole.ShowPassive(TextId.SelectedIzakaya.Get(display));

        IzakayaSelectorPanelPatch.TryProceedWithConfirmedSelection(
            action.MapLabel,
            (IzakayaLevel)action.MapLevel);
    }
}

using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class SelectIzakayaBehavior
{
    public static void Send(MapLabel mapLabel, int level) =>
        new SelectIzakayaAction { MapLabel = mapLabel, MapLevel = level }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<SelectIzakayaAction>(Handle);
    }

    private static void Handle(SelectIzakayaAction action)
    {
        PlayerManager.SetPeerIzakayaSelection(action.SenderUid, action.MapLabel, action.MapLevel);

        var peerName = LiveModeManager.GetDisplayName(action.SenderUid);
        InGameConsole.ShowPassive(TextId.PeerSelectedIzakaya.Get(
            $"{peerName}", action.MapLabel.FormatIzakayaSelection(action.MapLevel)));

        if (MpManager.IsRoomHost)
        {
            IzakayaSelectorPanelPatch.TryConfirmSelection();
        }
        else
        {
            IzakayaSelectorPanelPatch.ShowSelectionStatus();
        }
    }
}

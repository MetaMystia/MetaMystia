using Common.UI;
using MetaMystia.Patch;
using MetaMystia.Protocol.Messages.DayScene;
using MetaMystia.Protocol.Messages.WorkScene;
using MetaMystia.UI;

namespace MetaMystia.Network.Handlers;

[AutoLog]
public static partial class DaySceneHandlers
{
    public static void Register()
    {
        MessageDispatcher.Register<ConfirmIzakayaMessage>(HandleConfirmIzakaya);
        MessageDispatcher.Register<MoveSyncMessage>(HandleMoveSync);
        MessageDispatcher.Register<SelectIzakayaMessage>(HandleSelectIzakaya);
        MessageDispatcher.Register<NightMoveSyncMessage>(HandleNightMoveSync);
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleConfirmIzakaya(ConfirmIzakayaMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var display = $"{Utils.GetMapLabelNameCN(msg.MapLabel)} {Utils.GetMapLevelNameCN(msg.MapLevel)}";
            InGameConsole.ShowPassive(TextId.SelectedIzakaya.Get(display));

            IzakayaSelectorPanelPatch.TryProceedWithConfirmedSelection(msg.MapLabel, (IzakayaLevel)msg.MapLevel);
        });
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleMoveSync(MoveSyncMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            if (PlayerManager.TryGetVisiblePeer(msg.SenderUid, out var peer))
                peer.SyncFromPeer(msg.MapLabel, msg.IsSprinting, msg.Speed,
                    new UnityEngine.Vector2(msg.Vx, msg.Vy), new UnityEngine.Vector2(msg.Px, msg.Py));
        });
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleSelectIzakaya(SelectIzakayaMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            PlayerManager.SetPeerIzakayaSelection(msg.SenderUid, msg.MapLabel, msg.MapLevel);

            var peerName = LiveModeManager.GetDisplayName(msg.SenderUid);
            InGameConsole.ShowPassive(TextId.PeerSelectedIzakaya.Get(
                $"{peerName}", $"{Utils.GetMapLabelNameCN(msg.MapLabel)} {Utils.GetMapLevelNameCN(msg.MapLevel)}"));

            if (MpManager.IsServer)
            {
                IzakayaSelectorPanelPatch.TryConfirmSelection();
            }
            else
            {
                IzakayaSelectorPanelPatch.ShowSelectionStatus();
            }
        });
    }

    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleNightMoveSync(NightMoveSyncMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            if (PlayerManager.TryGetVisiblePeer(msg.SenderUid, out var peer))
                peer.NightSyncFromPeer(msg.Speed, new UnityEngine.Vector2(msg.Vx, msg.Vy), new UnityEngine.Vector2(msg.Px, msg.Py));
        });
    }
}

using Common.UI;
using MetaMystia.Network.Utilities;
using MetaMystia.Patch;
using MetaMystia.UI;
using MetaMystia.Protocol.Messages.Common;
using MetaMystia.Protocol.Transport;

namespace MetaMystia.Network.Handlers;

[AutoLog]
public static partial class CommonHandlers
{
    public static void Register()
    {
        MessageDispatcher.Register<DayAllReadyMessage>(HandleDayAllReady);
        MessageDispatcher.Register<DayReadyMessage>(HandleDayReady);
        MessageDispatcher.Register<ChatMessage>(HandleChatMessage);
        MessageDispatcher.Register<PlayerChangeSkinMessage>(HandlePlayerChangeSkin);
        MessageDispatcher.Register<SceneTransitMessage>(HandleSceneTransit);
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleDayAllReady(DayAllReadyMessage msg)
    {
        if (msg.SenderUid != MpConstants.HostUid)
        {
            Log.LogWarning($"DayAllReady from non-host uid={msg.SenderUid}, ignoring");
            return;
        }

        InGameConsole.ShowPassive(TextId.AllReadyTransition.Get());
        DaySceneManagerPatch.OnDayOver();
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleDayReady(DayReadyMessage msg)
    {
        PlayerManager.SetPeerDayOver(msg.SenderUid);
        MpManager.DayOver();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(msg.SenderUid)));
    }

    [HandlerAttributes.CheckScene(Scene.DayScene)]
    public static void HandleChatMessage(ChatMessage msg)
    {
        var senderName = PlayerManager.GetPeerName(msg.SenderUid);
        InGameConsole.AddPeerMessage(senderName, msg.Message);
        if (!LiveModeManager.SuppressFloatingChatBubbles
            && PlayerManager.TryGetVisiblePeer(msg.SenderUid, out var senderPeer)
            && PlayerManager.LocalMapLabel == senderPeer.MapLabel)
        {
            FloatingTextHelper.ShowFloatingTextOnMainThread(
                senderPeer.GetCharacterUnit(), LiveModeManager.MaskMessage(msg.Message));
        }
    }
    public static void HandlePlayerChangeSkin(PlayerChangeSkinMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            if (!PlayerManager.TryGetVisiblePeer(msg.SenderUid, out var peer))
            {
                return;
            }

            peer.Skin = msg.Skin;
            peer.UpdateCharacterSprite();
            if (!string.IsNullOrEmpty(msg.Skin?.NetSkinName))
                NetSkinManager.RequestSkin(msg.Skin.NetSkinName);
        });
    }

    public static void HandleSceneTransit(SceneTransitMessage msg)
    {
        MpManager.PeerScene = EnumConverter.ToGame(msg.Scene);
    }
}

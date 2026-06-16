using Common.UI;
using MetaMystia.Protocol.Messages.Common;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Data;
using MetaMystia.UI;

namespace MetaMystia.Network.Services;

public static class CommonServices
{
     public static void SendDayAllReady()
     {
         if (!MpManager.IsRoomHost) return;
         var msg = new DayAllReadyMessage();
         MpWire.Send(msg);
     }

     public static void SendDayReady()
     {
         MpWire.Send(new DayReadyMessage());
     }

     public static void SendChat(string message)
     {
         if (!LiveModeManager.SuppressFloatingChatBubbles)
             FloatingTextHelper.ShowFloatingTextSelfOnMainThread(LiveModeManager.MaskMessage(message));
         MpWire.Send(ChatMessage.Create(message));
     }

     public static void SendPlayerChangeSkin(PlayerSkinData skin)
     {
         var msg = new PlayerChangeSkinMessage { Skin = skin };
         MpWire.Send(msg);
     }

    public static void SendSceneTransit(Scene scene)
    {
        var msg = new SceneTransitMessage{Scene=EnumConverter.ToProtocol(scene)};
        MpWire.Send(msg);
    }
}

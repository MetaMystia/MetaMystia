using MemoryPack;

using MetaMystia.Network;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 玩家或服务端 → 所有玩家：发送聊天消息
/// </summary>
[MemoryPackable]
public partial class ChatMessage : MultiplayerMessage
{

    public string Message { get; set; }
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Message;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Message;

    public override void OnReceivedDerived()
    {
        if (SenderUid == 0)
        {
            InGameConsole.AddChat(TextId.ServerChatName.Get(), Message, fromServer: true);
            return;
        }
        if (SenderUid == GameSession.Client.Uid)
        {
            InGameConsole.AddChat(LiveModeManager.GetLocalDisplayName(), Message);
            if (!LiveModeManager.SuppressFloatingChatBubbles)
                FloatingTextHelper.ShowFloatingTextSelfOnMainThread(LiveModeManager.MaskMessage(Message));
            return;
        }
        var senderName = PlayerManager.GetPeerName(SenderUid);
        InGameConsole.AddChat(senderName, Message);
        if (!LiveModeManager.SuppressFloatingChatBubbles
            && PlayerManager.TryGetVisiblePeer(SenderUid, out var senderPeer)
            && PlayerManager.LocalMapLabel == senderPeer.MapLabel)
        {
            FloatingTextHelper.ShowFloatingTextOnMainThread(
                senderPeer.GetCharacterUnit(), LiveModeManager.MaskMessage(Message));
        }
    }
    public static void Send(string message) =>
        new ChatMessage { Message = message.Length <= ChatPayload.MaxLength ? message : message[..ChatPayload.MaxLength] }.Enqueue();
}

using System.Data.Common;
using MemoryPack;

using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：发送聊天消息
/// </summary>
[MemoryPackable]
[PublicRelay]
public partial class MessageAction : Action
{

    [MemoryPackIgnore]
    private const int maxMessageLen = 1024;
    public string Message { get; private set; }
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Message;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Message;

    public override void OnReceivedDerived()
    {
        var senderName = PlayerManager.GetPeerName(SenderUid);
        InGameConsole.AddPeerMessage(senderName, Message);
        if (!LiveModeManager.SuppressFloatingChatBubbles
            && PlayerManager.TryGetVisiblePeer(SenderUid, out var senderPeer)
            && PlayerManager.LocalMapLabel == senderPeer.MapLabel)
        {
            FloatingTextHelper.ShowFloatingTextOnMainThread(
                senderPeer.GetCharacterUnit(), LiveModeManager.MaskMessage(Message));
        }
    }
    private static MessageAction CreateMsgAction(string msg)
    {
        if (msg.Length <= maxMessageLen)
        {
            return new MessageAction { Message = msg };
        }
        else
        {
            return new MessageAction { Message = msg[..maxMessageLen] };
        }
    }

    public static void Send(string message)
    {
        if (!LiveModeManager.SuppressFloatingChatBubbles)
            FloatingTextHelper.ShowFloatingTextSelfOnMainThread(LiveModeManager.MaskMessage(message));
        CreateMsgAction(message).Enqueue();
    }
}

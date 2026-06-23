using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class MessageBehavior
{
    private const int MaxMessageLen = 1024;

    private static MessageAction CreateMsgAction(string msg) =>
        msg.Length <= MaxMessageLen
            ? new MessageAction { Message = msg }
            : new MessageAction { Message = msg[..MaxMessageLen] };

    public static void Send(string message)
    {
        if (!LiveModeManager.SuppressFloatingChatBubbles)
            FloatingTextHelper.ShowFloatingTextSelfOnMainThread(LiveModeManager.MaskMessage(message));
        CreateMsgAction(message).Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<MessageAction>(Handle);
    }

    private static void Handle(MessageAction action)
    {
        var senderName = PlayerManager.GetPeerName(action.SenderUid);
        InGameConsole.AddPeerMessage(senderName, action.Message);
        if (!LiveModeManager.SuppressFloatingChatBubbles
            && PlayerManager.TryGetVisiblePeer(action.SenderUid, out var senderPeer)
            && PlayerManager.LocalMapLabel == senderPeer.MapLabel)
        {
            FloatingTextHelper.ShowFloatingTextOnMainThread(
                senderPeer.GetCharacterUnit(), LiveModeManager.MaskMessage(action.Message));
        }
    }
}

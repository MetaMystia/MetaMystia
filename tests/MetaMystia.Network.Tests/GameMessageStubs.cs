using MemoryPack;

using MetaMystia.Network;

namespace MetaMystia.Multiplayer
{
    // 使用实际 GameMessages 收发入口；仅替代游戏状态和消息执行副作用。
    public static class GameSession
    {
        public static Client Client { get; set; } = null!;
        public static bool IsOnline => Client.IsConnected;
        public static Room? Room => Client.State.Room;
        public static bool IsInRoom => Room != null;
        public static bool IsRoomHost => Room?.Host == Client.Uid;
        public static void Stop() => Client.Disconnect();
    }
}

namespace MetaMystia.Multiplayer.Messages
{
    [MemoryPackable]
    [MemoryPackUnion((ushort)GameMessageType.Chat, typeof(ChatMessage))]
    [MemoryPackUnion((ushort)GameMessageType.NightCook, typeof(NightCookMessage))]
    [MemoryPackUnion((ushort)GameMessageType.Emote, typeof(EmoteMessage))]
    public abstract partial class MultiplayerMessage
    {
        [MemoryPackIgnore] public int SenderUid { get; set; }
        [MemoryPackIgnore] public int? WireTargetUid { get; set; }
        [MemoryPackIgnore] public MessageContext Context { get; set; } = null!;
        [MemoryPackIgnore] public Client Connection { get; set; } = null!;
        public static readonly List<(int Recipient, int Sender)> Delivered = [];
        public void OnReceived() => Delivered.Add((Connection.Uid, SenderUid));
    }

    [MemoryPackable]
    public partial class ChatMessage : MultiplayerMessage { public string Message { get; set; } = "hello"; }
    [MemoryPackable]
    public partial class NightCookMessage : MultiplayerMessage {}
    public class ServeSellableMessage : MultiplayerMessage { public int ActorUid { get; set; } }
    public class ConfirmServeMessage : MultiplayerMessage { public int ActorUid { get; set; } }
    [MemoryPackable]
    public partial class EmoteMessage : MultiplayerMessage {}
}

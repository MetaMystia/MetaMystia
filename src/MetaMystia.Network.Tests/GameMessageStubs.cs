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
    [MemoryPackUnion((ushort)GameMessageType.Ping, typeof(PingMessage))]
    [MemoryPackUnion((ushort)GameMessageType.NightCook, typeof(NightCookMessage))]
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
    public partial class PingMessage : MultiplayerMessage {}
    public class PongMessage : MultiplayerMessage {}
    [MemoryPackable]
    public partial class ChatMessage : MultiplayerMessage {}
    public class SelectIzakayaMessage : MultiplayerMessage {}
    public class ConfirmIzakayaMessage : MultiplayerMessage {}
    public class UpdatePrepMessage : MultiplayerMessage {}
    public class PrepReadyMessage : MultiplayerMessage {}
    public class PrepAllReadyMessage : MultiplayerMessage {}
    [MemoryPackable]
    public partial class NightCookMessage : MultiplayerMessage {}
    public class ExtractFromCookerMessage : MultiplayerMessage {}
    public class StoreFoodMessage : MultiplayerMessage {}
    public class StoreSellableMessage : MultiplayerMessage {}
    public class ExtractFoodMessage : MultiplayerMessage {}
    public class QTEMessage : MultiplayerMessage {}
    public class BuffMessage : MultiplayerMessage {}
    public class GuestInviteMessage : MultiplayerMessage {}
    public class GuestSpawnMessage : MultiplayerMessage {}
    public class MoveToDeskMessage : MultiplayerMessage {}
    public class MoveToQueueMessage : MultiplayerMessage {}
    public class PlayerRepellMessage : MultiplayerMessage {}
    public class GenerateOrderMessage : MultiplayerMessage {}
    public class ServeSellableMessage : MultiplayerMessage { public int ActorUid { get; set; } }
    public class EvaluateOrderMessage : MultiplayerMessage {}
    public class ConfirmServeMessage : MultiplayerMessage { public int ActorUid { get; set; } }
    public class GuestLeaveMessage : MultiplayerMessage {}
    public class SendFromQueueMessage : MultiplayerMessage {}
    public class PatientDepletedQueueMessage : MultiplayerMessage {}
    public class PatientDepletedDeskMessage : MultiplayerMessage {}
    public class GuestKillMessage : MultiplayerMessage {}
    public class FundEditMessage : MultiplayerMessage {}
    public class TipEditMessage : MultiplayerMessage {}
    public class ExpEditMessage : MultiplayerMessage {}
    public class PassionEditMessage : MultiplayerMessage {}
    public class IzakayaCloseMessage : MultiplayerMessage {}
    public class GuestRepellMessage : MultiplayerMessage {}
    public class YuyukoFailedMessage : MultiplayerMessage {}
    public class YuyukoLifeMessage : MultiplayerMessage {}
    public class DayDestinationIntentMessage : MultiplayerMessage {}
    public class DayDestinationStateMessage : MultiplayerMessage {}
    public class DayDestinationConfirmMessage : MultiplayerMessage {}
    public class YuyukoGuestMessage : MultiplayerMessage {}
    public class YuyukoGuestBoundMessage : MultiplayerMessage {}
    public class RoomInitialStateMessage : MultiplayerMessage {}
    public class BusinessStartMessage : MultiplayerMessage {}
}

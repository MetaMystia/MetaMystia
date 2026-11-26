using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using MemoryPack;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

namespace MetaMystia.Multiplayer;

public static class GameMessages
{
    private static readonly Dictionary<GameMessageType, MessageRule> rules = GameMessageRules.Create().ToDictionary(r => (GameMessageType)r.Id);
    private static readonly Dictionary<Type, GameMessageType> types = typeof(MultiplayerMessage)
        .GetCustomAttributes<MemoryPackUnionAttribute>().ToDictionary(a => a.Type, a => (GameMessageType)a.Tag);
    public static GameMessageType TypeOf(MultiplayerMessage message) => types[message.GetType()];

    public static bool CanSend(MultiplayerMessage message) => GameSession.IsOnline
        && (!rules[TypeOf(message)].RoomScoped || GameSession.IsInRoom);

    public static void Send(MultiplayerMessage message)
    {
        var type = TypeOf(message);
        var rule = rules[type];
        if (rule.HostOnly && !GameSession.IsRoomHost) return;
        var body = message is ChatMessage chat
            ? MemoryPackSerializer.Serialize(new ChatPayload { Message = chat.Message })
            : MemoryPackSerializer.Serialize(message);
        var route = message.WireTargetUid != null ? Route.Player
            : !rule.RoomScoped ? Route.World
            : rule.Routes.Contains(Route.Host) && (!GameSession.IsRoomHost || !rule.Routes.Contains(Route.Room)) ? Route.Host
            : Route.Room;
        GameSession.Client.Send((ushort)type, body, route, message.WireTargetUid ?? 0);
    }

    public static void Receive(ReceivedMessage received)
    {
        MultiplayerMessage message;
        try
        {
            if (received.Type == (ushort)GameMessageType.Chat)
            {
                var chat = MemoryPackSerializer.Deserialize<ChatPayload>(received.Body);
                if (chat?.Message == null) return;
                message = new ChatMessage { Message = chat.Message };
            }
            else message = MemoryPackSerializer.Deserialize<MultiplayerMessage>(received.Body)!;
        }
        catch (MemoryPackSerializationException) { return; }
        if (message == null || (ushort)TypeOf(message) != received.Type
            || message is ChatMessage { Message: null }) return;
        message.SenderUid = received.Context.Sender;
        message.Context = received.Context;
        message.Connection = GameSession.Client;
        if (GameSession.IsRoomHost)
        {
            if (message is ServeSellableMessage serve) serve.ActorUid = message.SenderUid;
            if (message is ConfirmServeMessage confirm) confirm.ActorUid = message.SenderUid;
        }
        message.OnReceived();
    }
}

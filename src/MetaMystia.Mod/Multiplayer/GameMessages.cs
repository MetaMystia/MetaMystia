using System;
using System.Collections.Generic;
using System.Linq;

using MemoryPack;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

namespace MetaMystia.Multiplayer;

public static class GameMessages
{
    private static readonly Dictionary<GameMessageType, MessageRule> rules = GameMessageRules.Create().ToDictionary(r => (GameMessageType)r.Id);
    public static GameMessageType TypeOf(MultiplayerMessage message) => message switch
    {
        PingMessage => GameMessageType.Ping,
        PongMessage => GameMessageType.Pong,
        ChatMessage => GameMessageType.Chat,
        SelectIzakayaMessage => GameMessageType.SelectIzakaya,
        ConfirmIzakayaMessage => GameMessageType.ConfirmIzakaya,
        UpdatePrepMessage => GameMessageType.UpdatePrep,
        PrepReadyMessage => GameMessageType.PrepReady,
        PrepAllReadyMessage => GameMessageType.PrepAllReady,
        NightCookMessage => GameMessageType.NightCook,
        ExtractFromCookerMessage => GameMessageType.ExtractFromCooker,
        StoreFoodMessage => GameMessageType.StoreFood,
        StoreSellableMessage => GameMessageType.StoreSellable,
        ExtractFoodMessage => GameMessageType.ExtractFood,
        QTEMessage => GameMessageType.QTE,
        BuffMessage => GameMessageType.Buff,
        GuestInviteMessage => GameMessageType.GuestInvite,
        GuestSpawnMessage => GameMessageType.GuestSpawn,
        MoveToDeskMessage => GameMessageType.MoveToDesk,
        MoveToQueueMessage => GameMessageType.MoveToQueue,
        PlayerRepellMessage => GameMessageType.PlayerRepell,
        GenerateOrderMessage => GameMessageType.GenerateOrder,
        ServeSellableMessage => GameMessageType.ServeSellable,
        EvaluateOrderMessage => GameMessageType.EvaluateOrder,
        ConfirmServeMessage => GameMessageType.ConfirmServe,
        GuestLeaveMessage => GameMessageType.GuestLeave,
        SendFromQueueMessage => GameMessageType.SendFromQueue,
        PatientDepletedQueueMessage => GameMessageType.PatientDepletedQueue,
        PatientDepletedDeskMessage => GameMessageType.PatientDepletedDesk,
        GuestKillMessage => GameMessageType.GuestKill,
        FundEditMessage => GameMessageType.FundEdit,
        TipEditMessage => GameMessageType.TipEdit,
        ExpEditMessage => GameMessageType.ExpEdit,
        PassionEditMessage => GameMessageType.PassionEdit,
        IzakayaCloseMessage => GameMessageType.IzakayaClose,
        GuestRepellMessage => GameMessageType.GuestRepell,
        YuyukoFailedMessage => GameMessageType.YuyukoFailed,
        YuyukoLifeMessage => GameMessageType.YuyukoLife,
        DayDestinationIntentMessage => GameMessageType.DayDestinationIntent,
        DayDestinationStateMessage => GameMessageType.DayDestinationState,
        DayDestinationConfirmMessage => GameMessageType.DayDestinationConfirm,
        YuyukoGuestMessage => GameMessageType.YuyukoGuest,
        YuyukoGuestBoundMessage => GameMessageType.YuyukoGuestBound,
        RoomInitialStateMessage => GameMessageType.RoomInitialState,
        BusinessStartMessage => GameMessageType.BusinessStart,
        _ => throw new ArgumentException("Unregistered message", nameof(message))
    };

    public static bool CanSend(MultiplayerMessage message) => GameSession.IsOnline
        && (!rules[TypeOf(message)].RoomScoped || GameSession.IsInRoom);

    public static void Send(MultiplayerMessage message)
    {
        var type = TypeOf(message);
        var rule = rules[type];
        if (rule.HostOnly && !GameSession.IsRoomHost) return;
        var client = GameSession.Client;
        var body = MemoryPackSerializer.Serialize(message);
        if (message.WireTargetUid is int target) client.SendToPlayer(target, (ushort)type, body);
        else if (!rule.RoomScoped) client.SendToWorld((ushort)type, body);
        else if (rule.Routes.Contains(Route.Host) && (!GameSession.IsRoomHost || !rule.Routes.Contains(Route.Room)))
            client.SendToHost((ushort)type, body);
        else client.SendToRoom((ushort)type, body);
    }

    public static void Receive(ReceivedMessage received)
    {
        MultiplayerMessage message;
        try { message = MemoryPackSerializer.Deserialize<MultiplayerMessage>(received.Body); }
        catch (MemoryPackSerializationException) { return; }
        if (message == null || (ushort)TypeOf(message) != received.Type) return;
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

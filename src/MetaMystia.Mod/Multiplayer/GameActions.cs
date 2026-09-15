using System;
using System.Collections.Generic;
using System.Linq;

using MemoryPack;

using MetaMystia.Multiplayer.Actions;
using MetaMystia.Network;

using Action = MetaMystia.Multiplayer.Actions.Action;

namespace MetaMystia.Multiplayer;

public static class GameActions
{
    private static readonly Dictionary<GameMessage, MessageRule> rules = GameMessageRules.Create().ToDictionary(r => (GameMessage)r.Id);
    public static GameMessage TypeOf(Action action) => action switch
    {
        PingAction => GameMessage.Ping,
        PongAction => GameMessage.Pong,
        MessageAction => GameMessage.Message,
        SelectIzakayaAction => GameMessage.SelectIzakaya,
        ConfirmIzakayaAction => GameMessage.ConfirmIzakaya,
        UpdatePrepAction => GameMessage.UpdatePrep,
        PrepReadyAction => GameMessage.PrepReady,
        PrepAllReadyAction => GameMessage.PrepAllReady,
        NightCookAction => GameMessage.NightCook,
        ExtractFromCookerAction => GameMessage.ExtractFromCooker,
        StoreFoodAction => GameMessage.StoreFood,
        StoreSellableAction => GameMessage.StoreSellable,
        ExtractFoodAction => GameMessage.ExtractFood,
        QTEAction => GameMessage.QTE,
        BuffAction => GameMessage.Buff,
        GuestInviteAction => GameMessage.GuestInvite,
        GuestSpawnAction => GameMessage.GuestSpawn,
        MoveToDeskAction => GameMessage.MoveToDesk,
        MoveToQueueAction => GameMessage.MoveToQueue,
        PlayerRepellAction => GameMessage.PlayerRepell,
        GenerateOrderAction => GameMessage.GenerateOrder,
        ServeSellableAction => GameMessage.ServeSellable,
        EvaluateOrderAction => GameMessage.EvaluateOrder,
        ConfirmServeAction => GameMessage.ConfirmServe,
        GuestLeaveAction => GameMessage.GuestLeave,
        SendFromQueueAction => GameMessage.SendFromQueue,
        PatientDepletedQueueAction => GameMessage.PatientDepletedQueue,
        PatientDepletedDeskAction => GameMessage.PatientDepletedDesk,
        GuestKillAction => GameMessage.GuestKill,
        FundEditAction => GameMessage.FundEdit,
        TipEditAction => GameMessage.TipEdit,
        ExpEditAction => GameMessage.ExpEdit,
        PassionEditAction => GameMessage.PassionEdit,
        IzakayaCloseAction => GameMessage.IzakayaClose,
        GuestRepellAction => GameMessage.GuestRepell,
        YuyukoFailedAction => GameMessage.YuyukoFailed,
        YuyukoLifeAction => GameMessage.YuyukoLife,
        DayDestinationIntentAction => GameMessage.DayDestinationIntent,
        DayDestinationStateAction => GameMessage.DayDestinationState,
        DayDestinationConfirmAction => GameMessage.DayDestinationConfirm,
        YuyukoGuestAction => GameMessage.YuyukoGuest,
        YuyukoGuestBoundAction => GameMessage.YuyukoGuestBound,
        RoomReadyAction => GameMessage.RoomReady,
        _ => throw new ArgumentException("Unregistered action", nameof(action))
    };

    public static bool CanSend(Action action) => GameSession.IsOnline
        && (!rules[TypeOf(action)].RoomScoped || GameSession.IsInRoom);

    public static void Send(Action action)
    {
        var type = TypeOf(action);
        var rule = rules[type];
        if (rule.HostOnly && !GameSession.IsRoomHost) return;
        var client = GameSession.Client;
        var body = MemoryPackSerializer.Serialize(action);
        if (action.WireTargetUid is int target) client.SendToPlayer(target, (ushort)type, body);
        else if (!rule.RoomScoped) client.SendToWorld((ushort)type, body);
        else if (rule.Routes.Contains(Route.Host) && (!GameSession.IsRoomHost || !rule.Routes.Contains(Route.Room)))
            client.SendToHost((ushort)type, body);
        else client.SendToRoom((ushort)type, body);
    }

    public static void Receive(ReceivedMessage received)
    {
        Action action;
        try { action = MemoryPackSerializer.Deserialize<Action>(received.Body); }
        catch (MemoryPackSerializationException) { GameSession.Stop(); return; }
        if (action == null || (ushort)TypeOf(action) != received.Type) { GameSession.Stop(); return; }
        action.SenderUid = received.Context.Sender;
        action.Context = received.Context;
        action.Connection = GameSession.Client;
        if (GameSession.IsRoomHost)
        {
            if (action is ServeSellableAction serve) serve.ActorUid = action.SenderUid;
            if (action is ConfirmServeAction confirm) confirm.ActorUid = action.SenderUid;
        }
        action.OnReceived();
    }
}

using System;
using System.Collections.Generic;

using MemoryPack;

using MetaMystia.Protocol;

namespace MetaMystia.Network;

[AutoLog]
public static partial class GameMessages
{
    public const int Version = 1;
    private sealed record Entry(ushort Id, Route Route, Func<Action, byte[]> Encode, Func<byte[], Action> Decode,
        bool PhaseIndependent = false, bool RequestAndResult = false, GameplayPhase RequiredPhase = GameplayPhase.Night);
    private static readonly Dictionary<Type, Entry> Types = new();
    private static readonly Dictionary<ushort, Entry> Kinds = new();

    static GameMessages()
    {
        Add<PlayerChangeSkinAction>(1, Route.PublicState, phaseIndependent: true);
        Add<SceneTransitAction>(2, Route.PublicState, phaseIndependent: true);
        Add<MoveSyncAction>(3, Route.PublicState, phaseIndependent: true);
        Add<MessageAction>(4, Route.PublicEvent, phaseIndependent: true);
        Add<ResourceSnapshotAction>(5, Route.MemberState, phaseIndependent: true);
        Add<GameplaySnapshotAction>(6, Route.HostState, phaseIndependent: true);
        Add<DayReadyAction>(7, Route.HostRequest, phase: GameplayPhase.Day);
        Add<SelectIzakayaAction>(8, Route.HostRequest, phase: GameplayPhase.Selection);
        Add<PrepReadyAction>(9, Route.HostRequest, phase: GameplayPhase.Prep);
        Add<UpdatePrepAction>(10, Route.MemberEvent, phase: GameplayPhase.Prep);
        Add<GuestInviteAction>(11, Route.HostRequest, phase: GameplayPhase.Selection);
        Add<NightMoveSyncAction>(12, Route.MemberState);
        Add<NightCookAction>(20, Route.MemberEvent);
        Add<ExtractFromCookerAction>(21, Route.MemberEvent);
        Add<StoreFoodAction>(22, Route.MemberEvent);
        Add<StoreSellableAction>(23, Route.MemberEvent);
        Add<ExtractFoodAction>(24, Route.MemberEvent);
        Add<QTEAction>(25, Route.MemberEvent);
        Add<BuffAction>(26, Route.MemberEvent);
        Add<PlayerRepellAction>(27, Route.MemberEvent);
        Add<ServeSellableAction>(28, Route.HostRequest, requestAndResult: true);
        Add<ConfirmServeAction>(29, Route.HostRequest, requestAndResult: true);
        Add<GuestSpawnAction>(30, Route.HostEvent);
        Add<MoveToDeskAction>(31, Route.HostEvent);
        Add<MoveToQueueAction>(32, Route.HostEvent);
        Add<GenerateOrderAction>(33, Route.HostEvent);
        Add<EvaluateOrderAction>(34, Route.HostEvent);
        Add<GuestLeaveAction>(35, Route.HostEvent);
        Add<SendFromQueueAction>(36, Route.HostEvent);
        Add<PatientDepletedQueueAction>(37, Route.HostEvent);
        Add<PatientDepletedDeskAction>(38, Route.HostEvent);
        Add<GuestKillAction>(39, Route.HostEvent);
        Add<FundEditAction>(40, Route.HostEvent);
        Add<TipEditAction>(41, Route.HostEvent);
        Add<ExpEditAction>(42, Route.HostEvent);
        Add<PassionEditAction>(43, Route.HostEvent);
        Add<IzakayaCloseAction>(44, Route.HostEvent);
    }

    private static void Add<T>(ushort id, Route route, bool phaseIndependent = false, bool requestAndResult = false, GameplayPhase phase = GameplayPhase.Night) where T : Action
    {
        var entry = new Entry(id, route, action => MemoryPackSerializer.Serialize((T)action),
            bytes => MemoryPackSerializer.Deserialize<T>(bytes), phaseIndependent, requestAndResult, phase);
        Types.Add(typeof(T), entry);
        Kinds.Add(id, entry);
    }

    public static bool CanSend(Action action)
    {
        if (!Types.TryGetValue(action.GetType(), out var entry) || !MpWire.Session.IsOnline) return false;
        if (Payload.IsPublic(entry.Route)) return true;
        if (!MpWire.Session.CanPlay) return false;
        if (entry.Route is Route.HostEvent or Route.HostState && !MpWire.Session.IsRoomHost) return false;
        return entry.PhaseIndependent || RoomGameplay.IsReady && RoomGameplay.Phase == entry.RequiredPhase;
    }

    public static void Send(Action action)
    {
        if (!CanSend(action)) return;
        var entry = Types[action.GetType()];
        var route = entry.RequestAndResult && MpWire.Session.IsRoomHost ? Route.HostEvent : entry.Route;
        if (route is Route.PublicState or Route.MemberState)
        {
            action.SenderUid = MpWire.Session.SelfUid;
            action.OnReceivedDerived();
        }
        MpWire.Session.Send(route, entry.Id, entry.Encode(action), entry.PhaseIndependent ? 0 : RoomGameplay.PhaseId, action.WireTargetUid ?? -1);
    }

    public static void Receive(Payload payload)
    {
        if (!Kinds.TryGetValue(payload.Kind, out var entry))
        {
            if (!Payload.IsPublic(payload.Route)) RoomGameplay.Abort($"Unknown payload {payload.Kind}");
            return;
        }
        bool validRoute = payload.Route == entry.Route || entry.RequestAndResult && payload.Route == Route.HostEvent;
        if (!validRoute || entry.PhaseIndependent != (payload.PhaseId == 0)) return;
        if (!entry.PhaseIndependent && (payload.PhaseId != RoomGameplay.PhaseId || RoomGameplay.Phase != entry.RequiredPhase)) return;
        Action action;
        try { action = entry.Decode(payload.Data); }
        catch (MemoryPackSerializationException error)
        {
            Log.Warning($"Invalid payload {payload.Kind}: {error.Message}");
            if (!Payload.IsPublic(payload.Route)) RoomGameplay.Abort($"Invalid payload {payload.Kind}");
            return;
        }
        if (action == null) return;
        action.SenderUid = payload.SenderUid;
        action.Binding = MpWire.Session.Binding;
        action.PhaseId = payload.PhaseId;
        if (action is ServeSellableAction serve && payload.Route == Route.HostRequest) serve.ActorUid = payload.SenderUid;
        if (action is ConfirmServeAction confirm && payload.Route == Route.HostRequest) confirm.ActorUid = payload.SenderUid;
        if (entry.PhaseIndependent || action is DayReadyAction or PrepReadyAction or SelectIzakayaAction or NightMoveSyncAction)
            action.OnReceived();
        else RoomGameplay.ReceiveEffect(action);
    }
}

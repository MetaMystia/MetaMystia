using System;
using System.Collections.Concurrent;
using System.Reflection;

using BepInEx.Logging;
using MemoryPack;

using MetaMystia.Network;

namespace MetaMystia.Multiplayer.Messages;



[MemoryPackable]
[MemoryPackUnion((ushort)GameMessageType.Chat, typeof(ChatMessage))]
[MemoryPackUnion((ushort)GameMessageType.SelectIzakaya, typeof(SelectIzakayaMessage))]
[MemoryPackUnion((ushort)GameMessageType.ConfirmIzakaya, typeof(ConfirmIzakayaMessage))]
[MemoryPackUnion((ushort)GameMessageType.UpdatePrep, typeof(UpdatePrepMessage))]
[MemoryPackUnion((ushort)GameMessageType.PrepReady, typeof(PrepReadyMessage))]
[MemoryPackUnion((ushort)GameMessageType.PrepAllReady, typeof(PrepAllReadyMessage))]
[MemoryPackUnion((ushort)GameMessageType.NightCook, typeof(NightCookMessage))]
[MemoryPackUnion((ushort)GameMessageType.ExtractFromCooker, typeof(ExtractFromCookerMessage))]
[MemoryPackUnion((ushort)GameMessageType.StoreFood, typeof(StoreFoodMessage))]
[MemoryPackUnion((ushort)GameMessageType.StoreSellable, typeof(StoreSellableMessage))]
[MemoryPackUnion((ushort)GameMessageType.ExtractFood, typeof(ExtractFoodMessage))]
[MemoryPackUnion((ushort)GameMessageType.QTE, typeof(QTEMessage))]
[MemoryPackUnion((ushort)GameMessageType.Buff, typeof(BuffMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestInvite, typeof(GuestInviteMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestSpawn, typeof(GuestSpawnMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestSeat, typeof(GuestSeatMessage))]
[MemoryPackUnion((ushort)GameMessageType.MoveToQueue, typeof(MoveToQueueMessage))]
[MemoryPackUnion((ushort)GameMessageType.PlayerRepell, typeof(PlayerRepellMessage))]
[MemoryPackUnion((ushort)GameMessageType.GenerateOrder, typeof(GenerateOrderMessage))]
[MemoryPackUnion((ushort)GameMessageType.ServeSellable, typeof(ServeSellableMessage))]
[MemoryPackUnion((ushort)GameMessageType.EvaluateOrder, typeof(EvaluateOrderMessage))]
[MemoryPackUnion((ushort)GameMessageType.ConfirmServe, typeof(ConfirmServeMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestLeave, typeof(GuestLeaveMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestStats, typeof(GuestStatsMessage))]
[MemoryPackUnion((ushort)GameMessageType.ServeReject, typeof(ServeRejectMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestQuery, typeof(GuestQueryMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestSnapshot, typeof(GuestSnapshotMessage))]
[MemoryPackUnion((ushort)GameMessageType.FundEdit, typeof(FundEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.TipEdit, typeof(TipEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.ExpEdit, typeof(ExpEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.PassionEdit, typeof(PassionEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.IzakayaClose, typeof(IzakayaCloseMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoFailed, typeof(YuyukoFailedMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoLife, typeof(YuyukoLifeMessage))]
[MemoryPackUnion((ushort)GameMessageType.DayDestinationIntent, typeof(DayDestinationIntentMessage))]
[MemoryPackUnion((ushort)GameMessageType.DayDestinationState, typeof(DayDestinationStateMessage))]
[MemoryPackUnion((ushort)GameMessageType.DayDestinationConfirm, typeof(DayDestinationConfirmMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoGuest, typeof(YuyukoGuestMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoGuestBound, typeof(YuyukoGuestBoundMessage))]
[MemoryPackUnion((ushort)GameMessageType.RoomInitialState, typeof(RoomInitialStateMessage))]
[MemoryPackUnion((ushort)GameMessageType.BusinessStart, typeof(BusinessStartMessage))]
[MemoryPackUnion((ushort)GameMessageType.Emote, typeof(EmoteMessage))]
[MemoryPackUnion((ushort)GameMessageType.TimeStopRequest, typeof(TimeStopRequestMessage))]
[MemoryPackUnion((ushort)GameMessageType.TimeStopState, typeof(TimeStopStateMessage))]
[MemoryPackUnion((ushort)GameMessageType.VersusState, typeof(VersusStateMessage))]
[MemoryPackUnion((ushort)GameMessageType.VersusChoice, typeof(VersusChoiceMessage))]
[MemoryPackUnion((ushort)GameMessageType.VersusEnd, typeof(VersusEndMessage))]
public abstract partial class MultiplayerMessage
{
    /// <summary>
    /// 服务器根据真实连接提供的发送者 UID，不进入玩法正文。
    /// </summary>
    [MemoryPackIgnore] public int SenderUid { get; set; }

    [MemoryPackIgnore] public int? WireTargetUid { get; set; }
    [MemoryPackIgnore] public MessageContext Context { get; set; }
    [MemoryPackIgnore, System.Text.Json.Serialization.JsonIgnore] public Client Connection { get; set; }
    [MemoryPackIgnore] protected bool IsCurrent => Connection == GameSession.Client && Connection?.IsCurrent(Context) == true;


    private LogLevel DefaultLogLevel => this is
        TipEditMessage or FundEditMessage or ExpEditMessage or PassionEditMessage or
        StoreFoodMessage or StoreSellableMessage or ExtractFoodMessage or ExtractFromCookerMessage or
        NightCookMessage or QTEMessage or BuffMessage or GuestSpawnMessage or GuestSeatMessage or
        MoveToQueueMessage or GenerateOrderMessage or ServeSellableMessage or ConfirmServeMessage or
        EvaluateOrderMessage or GuestLeaveMessage or PlayerRepellMessage ? LogLevel.Debug : LogLevel.Info;

    [MemoryPackIgnore]
    protected virtual LogLevel OnReceiveLogLevel => DefaultLogLevel;

    [MemoryPackIgnore]
    protected virtual LogLevel OnSendLogLevel => DefaultLogLevel;

    [MemoryPackIgnore]
    protected virtual bool OnReceiveLogOnlyMessage { get; } = false;

    [MemoryPackIgnore]
    protected virtual bool OnSendLogOnlyMessage { get; } = false;

    protected MultiplayerMessage()
    {
        SenderUid = GameSession.Client?.Uid ?? 0;
    }


    private sealed record ReceiveRules(Common.UI.Scene? Scene, bool DiscardOnStory, bool RequireHostSender, bool ClientOnly, bool HostOnly);
    private static readonly ConcurrentDictionary<Type, ReceiveRules> receiveRules = new();
    private ReceiveRules Rules => receiveRules.GetOrAdd(GetType(), type =>
    {
        var method = type.GetMethod(nameof(OnReceivedDerived));
        return new(method.GetCustomAttribute<CheckSceneAttribute>()?.Scene, method.IsDefined(typeof(DiscardOnStoryAttribute)),
            method.IsDefined(typeof(RequireHostSenderAttribute)), method.IsDefined(typeof(ClientOnlyReceiveAttribute)),
            method.IsDefined(typeof(HostOnlyReceiveAttribute)));
    });

    public abstract void OnReceivedDerived();
    public void OnReceived()
    {
        LogMessageReceived();
        var rules = Rules;
        if (rules.Scene is { } scene && GameFlow.LocalScene != scene)
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Received in invalid scene: {MessageName}: {ToLogString()}");
            return;
        }
        if (rules.DiscardOnStory && GameFlow.InStory)
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Discarded (in story): {MessageName}");
            return;
        }
        if (rules.RequireHostSender && SenderUid != GameSession.Room?.Host)
        {
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {MessageName} from non-host uid={SenderUid}, ignoring");
            return;
        }
        if (rules.ClientOnly && GameSession.IsRoomHost) return;
        if (rules.HostOnly && !GameSession.IsRoomHost)
        {
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {MessageName} received by non-host, ignoring");
            return;
        }
        OnReceivedDerived();
    }

    private static readonly System.Text.Json.JsonSerializerOptions LogJsonOptions = new()
    {
        IncludeFields = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public override string ToString() => System.Text.Json.JsonSerializer.Serialize((object)this, LogJsonOptions);

    protected virtual string ToLogString()
    {
        return ToString();
    }

    private string MessageName => GetType().Name;

    protected void LogMessageReceived() =>
        Log.Write(OnReceiveLogLevel, $"{MetaMystia.UI.MultiplayerStatus.RoleTag} Received {MessageName}{(OnReceiveLogOnlyMessage ? "" : $": {ToLogString()}")}");

    protected void LogMessageSend() =>
        Log.Write(OnSendLogLevel, $"{MetaMystia.UI.MultiplayerStatus.RoleTag} Send {MessageName}{(OnSendLogOnlyMessage ? "" : $": {ToLogString()}")}");

    protected void Enqueue()
    {
        if (!GameMessages.CanSend(this)) return;
        if (Rules.DiscardOnStory && GameFlow.InStory)
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Will not send (in story): {MessageName}");
            return;
        }
        LogMessageSend();
        GameMessages.Send(this);
    }

    public static void RegisterAllFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<MultiplayerMessage>()) MemoryPackFormatterProvider.Register(new MultiplayerMessageFormatter());
        if (!MemoryPackFormatterProvider.IsRegistered<MultiplayerMessage[]>()) MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.ArrayFormatter<MultiplayerMessage>());
    }

    [AttributeUsage(AttributeTargets.Method)]
    protected class CheckSceneAttribute(Common.UI.Scene scene) : Attribute
    {
        public Common.UI.Scene Scene { get; } = scene;
    }

    [AttributeUsage(AttributeTargets.Method)]
    protected class DiscardOnStoryAttribute : Attribute { }

    /// <summary>仅处理当前房主的权威广播。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    protected class RequireHostSenderAttribute : Attribute { }

    /// <summary>仅客机处理；主机本地已是权威状态，忽略入站包。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    protected class ClientOnlyReceiveAttribute : Attribute { }

    /// <summary>仅房主处理客机请求。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    protected class HostOnlyReceiveAttribute : Attribute { }
}

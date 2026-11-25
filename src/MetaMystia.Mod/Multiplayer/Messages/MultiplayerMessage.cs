using System;
using System.Reflection;

using BepInEx.Logging;
using MemoryPack;

using MetaMystia.Network;

namespace MetaMystia.Multiplayer.Messages;



[MemoryPackable]
[MemoryPackUnion((ushort)GameMessageType.Ping, typeof(PingMessage))]
[MemoryPackUnion((ushort)GameMessageType.Pong, typeof(PongMessage))]
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

public abstract partial class MultiplayerMessage
{
    protected long TimestampMs { get; set; }
    /// <summary>
    /// 服务器根据真实连接提供的发送者 UID，不进入玩法正文。
    /// </summary>
    [MemoryPackIgnore] public int SenderUid { get; set; }

    [MemoryPackIgnore] public int? WireTargetUid { get; set; }
    [MemoryPackIgnore] public MessageContext Context { get; set; }
    [MemoryPackIgnore, System.Text.Json.Serialization.JsonIgnore] public Client Connection { get; set; }
    [MemoryPackIgnore] protected bool IsCurrent => Connection == GameSession.Client && Connection?.IsCurrent(Context) == true;


    [MemoryPackIgnore]
    protected virtual LogLevel OnReceiveLogLevel { get; } = LogLevel.Info;

    [MemoryPackIgnore]
    protected virtual LogLevel OnSendLogLevel { get; } = LogLevel.Info;

    [MemoryPackIgnore]
    protected virtual bool OnReceiveLogOnlyMessage { get; } = false;

    [MemoryPackIgnore]
    protected virtual bool OnSendLogOnlyMessage { get; } = false;

    protected MultiplayerMessage()
    {
        TimestampMs = RoomClock.Now;
        SenderUid = GameSession.Client?.Uid ?? 0;
    }


    public abstract void OnReceivedDerived();
    public void OnReceived()
    {
        LogMessageReceived();
        var targetScene = GetReceivedScene();
        if (targetScene != null && GameFlow.LocalScene != targetScene.Value)
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Received in invalid scene: {MessageName}: {ToLogString()}");
            return;
        }
        if (ShouldDiscardOnStory())
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Discarded (in story): {MessageName}");
            return;
        }
        if (!PassesReceiveGuards()) return;
        OnReceivedDerived();
    }

    private bool PassesReceiveGuards()
    {
        var method = GetType().GetMethod(nameof(OnReceivedDerived));

        if (method.GetCustomAttribute<RequireHostSenderAttribute>() != null
            && SenderUid != GameSession.Room?.Host)
        {
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {MessageName} from non-host uid={SenderUid}, ignoring");
            return false;
        }

        if (method.GetCustomAttribute<ClientOnlyReceiveAttribute>() != null && GameSession.IsRoomHost)
            return false;

        if (method.GetCustomAttribute<HostOnlyReceiveAttribute>() != null && !GameSession.IsRoomHost)
        {
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {MessageName} received by non-host, ignoring");
            return false;
        }

        return true;
    }

    private Common.UI.Scene? GetReceivedScene()
    {
        var method = this.GetType().GetMethod(nameof(OnReceivedDerived));
        var attr = method.GetCustomAttribute<CheckSceneAttribute>();
        return attr?.Scene;
    }

    private bool ShouldDiscardOnStory()
    {
        if (!GameFlow.InStory) return false;
        var method = this.GetType().GetMethod(nameof(OnReceivedDerived));
        return method.GetCustomAttribute<DiscardOnStoryAttribute>() != null;
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
        if (ShouldDiscardOnStory())
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

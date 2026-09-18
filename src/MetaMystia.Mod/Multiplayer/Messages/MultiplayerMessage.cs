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
[MemoryPackUnion((ushort)GameMessageType.MoveToDesk, typeof(MoveToDeskMessage))]
[MemoryPackUnion((ushort)GameMessageType.MoveToQueue, typeof(MoveToQueueMessage))]
[MemoryPackUnion((ushort)GameMessageType.PlayerRepell, typeof(PlayerRepellMessage))]
[MemoryPackUnion((ushort)GameMessageType.GenerateOrder, typeof(GenerateOrderMessage))]
[MemoryPackUnion((ushort)GameMessageType.ServeSellable, typeof(ServeSellableMessage))]
[MemoryPackUnion((ushort)GameMessageType.EvaluateOrder, typeof(EvaluateOrderMessage))]
[MemoryPackUnion((ushort)GameMessageType.ConfirmServe, typeof(ConfirmServeMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestLeave, typeof(GuestLeaveMessage))]
[MemoryPackUnion((ushort)GameMessageType.SendFromQueue, typeof(SendFromQueueMessage))]
[MemoryPackUnion((ushort)GameMessageType.PatientDepletedQueue, typeof(PatientDepletedQueueMessage))]
[MemoryPackUnion((ushort)GameMessageType.PatientDepletedDesk, typeof(PatientDepletedDeskMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestKill, typeof(GuestKillMessage))]
[MemoryPackUnion((ushort)GameMessageType.FundEdit, typeof(FundEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.TipEdit, typeof(TipEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.ExpEdit, typeof(ExpEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.PassionEdit, typeof(PassionEditMessage))]
[MemoryPackUnion((ushort)GameMessageType.IzakayaClose, typeof(IzakayaCloseMessage))]
[MemoryPackUnion((ushort)GameMessageType.GuestRepell, typeof(GuestRepellMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoFailed, typeof(YuyukoFailedMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoLife, typeof(YuyukoLifeMessage))]
[MemoryPackUnion((ushort)GameMessageType.DayDestinationIntent, typeof(DayDestinationIntentMessage))]
[MemoryPackUnion((ushort)GameMessageType.DayDestinationState, typeof(DayDestinationStateMessage))]
[MemoryPackUnion((ushort)GameMessageType.DayDestinationConfirm, typeof(DayDestinationConfirmMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoGuest, typeof(YuyukoGuestMessage))]
[MemoryPackUnion((ushort)GameMessageType.YuyukoGuestBound, typeof(YuyukoGuestBoundMessage))]
[MemoryPackUnion((ushort)GameMessageType.RoomInitialState, typeof(RoomInitialStateMessage))]
[MemoryPackUnion((ushort)GameMessageType.BusinessStart, typeof(BusinessStartMessage))]
[AutoLog]

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

    protected void QueueForGuest(GuestFSM guest, string name, Func<bool> execute) =>
        guest?.Enqueue(name, () => !IsCurrent || execute());


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
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {MessageName} from non-host uid={SenderUid}, ignoring", false);
            return false;
        }

        if (method.GetCustomAttribute<ClientOnlyReceiveAttribute>() != null && GameSession.IsRoomHost)
            return false;

        if (method.GetCustomAttribute<HostOnlyReceiveAttribute>() != null && !GameSession.IsRoomHost)
        {
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {MessageName} received by non-host, ignoring", false);
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
        if (!GameFlow.InStory || CanReceiveDuringStory) return false;
        var method = this.GetType().GetMethod(nameof(OnReceivedDerived));
        return method.GetCustomAttribute<DiscardOnStoryAttribute>() != null;
    }

    [MemoryPackIgnore]
    protected virtual bool CanReceiveDuringStory => false;

    public override string ToString()
    {
        return System.Text.Json.JsonSerializer.Serialize((object)this,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                IncludeFields = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
    }

    protected virtual string ToLogString()
    {
        return ToString();
    }

    private string MessageName => GetType().Name;

    private static void LogMessage(LogLevel logLevel, string logStr)
    {
        switch (logLevel)
        {
            case LogLevel.Debug:
                Log.Debug(logStr, false);
                break;
            case LogLevel.Warning:
                Log.Warning(logStr, false);
                break;
            case LogLevel.Error:
                Log.Error(logStr, false);
                break;
            case LogLevel.Fatal:
                Log.Fatal(logStr, false);
                break;
            case LogLevel.Message:
                Log.Message(logStr, false);
                break;
            default:
                Log.Info(logStr, false);
                break;
        }
    }

    protected void LogMessageReceived()
    {
        string logStr = $"{MetaMystia.UI.MultiplayerStatus.RoleTag} Received {MessageName}{(OnReceiveLogOnlyMessage ? "" : $": {ToLogString()}")}";
        LogMessage(OnReceiveLogLevel, logStr);
    }

    protected void LogMessageSend()
    {
        string logStr = $"{MetaMystia.UI.MultiplayerStatus.RoleTag} Send {MessageName}{(OnSendLogOnlyMessage ? "" : $": {ToLogString()}")}";
        LogMessage(OnSendLogLevel, logStr);
    }

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

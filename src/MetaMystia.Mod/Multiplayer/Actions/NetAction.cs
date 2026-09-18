using System;
using System.Reflection;

using BepInEx.Logging;
using MemoryPack;

using MetaMystia.Network;
using SgrYuki;

namespace MetaMystia.Multiplayer.Actions;



[MemoryPackable]
[MemoryPackUnion((ushort)GameMessage.Ping, typeof(PingAction))]
[MemoryPackUnion((ushort)GameMessage.Pong, typeof(PongAction))]
[MemoryPackUnion((ushort)GameMessage.Message, typeof(MessageAction))]
[MemoryPackUnion((ushort)GameMessage.SelectIzakaya, typeof(SelectIzakayaAction))]
[MemoryPackUnion((ushort)GameMessage.ConfirmIzakaya, typeof(ConfirmIzakayaAction))]
[MemoryPackUnion((ushort)GameMessage.UpdatePrep, typeof(UpdatePrepAction))]
[MemoryPackUnion((ushort)GameMessage.PrepReady, typeof(PrepReadyAction))]
[MemoryPackUnion((ushort)GameMessage.PrepAllReady, typeof(PrepAllReadyAction))]
[MemoryPackUnion((ushort)GameMessage.NightCook, typeof(NightCookAction))]
[MemoryPackUnion((ushort)GameMessage.ExtractFromCooker, typeof(ExtractFromCookerAction))]
[MemoryPackUnion((ushort)GameMessage.StoreFood, typeof(StoreFoodAction))]
[MemoryPackUnion((ushort)GameMessage.StoreSellable, typeof(StoreSellableAction))]
[MemoryPackUnion((ushort)GameMessage.ExtractFood, typeof(ExtractFoodAction))]
[MemoryPackUnion((ushort)GameMessage.QTE, typeof(QTEAction))]
[MemoryPackUnion((ushort)GameMessage.Buff, typeof(BuffAction))]
[MemoryPackUnion((ushort)GameMessage.GuestInvite, typeof(GuestInviteAction))]
[MemoryPackUnion((ushort)GameMessage.GuestSpawn, typeof(GuestSpawnAction))]
[MemoryPackUnion((ushort)GameMessage.MoveToDesk, typeof(MoveToDeskAction))]
[MemoryPackUnion((ushort)GameMessage.MoveToQueue, typeof(MoveToQueueAction))]
[MemoryPackUnion((ushort)GameMessage.PlayerRepell, typeof(PlayerRepellAction))]
[MemoryPackUnion((ushort)GameMessage.GenerateOrder, typeof(GenerateOrderAction))]
[MemoryPackUnion((ushort)GameMessage.ServeSellable, typeof(ServeSellableAction))]
[MemoryPackUnion((ushort)GameMessage.EvaluateOrder, typeof(EvaluateOrderAction))]
[MemoryPackUnion((ushort)GameMessage.ConfirmServe, typeof(ConfirmServeAction))]
[MemoryPackUnion((ushort)GameMessage.GuestLeave, typeof(GuestLeaveAction))]
[MemoryPackUnion((ushort)GameMessage.SendFromQueue, typeof(SendFromQueueAction))]
[MemoryPackUnion((ushort)GameMessage.PatientDepletedQueue, typeof(PatientDepletedQueueAction))]
[MemoryPackUnion((ushort)GameMessage.PatientDepletedDesk, typeof(PatientDepletedDeskAction))]
[MemoryPackUnion((ushort)GameMessage.GuestKill, typeof(GuestKillAction))]
[MemoryPackUnion((ushort)GameMessage.FundEdit, typeof(FundEditAction))]
[MemoryPackUnion((ushort)GameMessage.TipEdit, typeof(TipEditAction))]
[MemoryPackUnion((ushort)GameMessage.ExpEdit, typeof(ExpEditAction))]
[MemoryPackUnion((ushort)GameMessage.PassionEdit, typeof(PassionEditAction))]
[MemoryPackUnion((ushort)GameMessage.IzakayaClose, typeof(IzakayaCloseAction))]
[MemoryPackUnion((ushort)GameMessage.GuestRepell, typeof(GuestRepellAction))]
[MemoryPackUnion((ushort)GameMessage.YuyukoFailed, typeof(YuyukoFailedAction))]
[MemoryPackUnion((ushort)GameMessage.YuyukoLife, typeof(YuyukoLifeAction))]
[MemoryPackUnion((ushort)GameMessage.DayDestinationIntent, typeof(DayDestinationIntentAction))]
[MemoryPackUnion((ushort)GameMessage.DayDestinationState, typeof(DayDestinationStateAction))]
[MemoryPackUnion((ushort)GameMessage.DayDestinationConfirm, typeof(DayDestinationConfirmAction))]
[MemoryPackUnion((ushort)GameMessage.YuyukoGuest, typeof(YuyukoGuestAction))]
[MemoryPackUnion((ushort)GameMessage.YuyukoGuestBound, typeof(YuyukoGuestBoundAction))]
[MemoryPackUnion((ushort)GameMessage.RoomReady, typeof(RoomReadyAction))]
[MemoryPackUnion((ushort)GameMessage.BusinessStart, typeof(BusinessStartAction))]
[AutoLog]

public abstract partial class Action
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
    protected virtual bool OnReceiveLogOnlyAction { get; } = false;

    [MemoryPackIgnore]
    protected virtual bool OnSendLogOnlyAction { get; } = false;

    protected Action()
    {
        TimestampMs = RoomClock.Now;
        SenderUid = GameSession.Client?.Uid ?? 0;
    }


    public abstract void OnReceivedDerived();
    public void OnReceived()
    {
        LogActionReceived();
        var targetScene = GetReceivedScene();
        if (targetScene != null && GameFlow.LocalScene != targetScene.Value)
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Received in invalid scene: {ActionName}: {ToLogString()}");
            return;
        }
        if (ShouldDiscardOnStory())
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Discarded (in story): {ActionName}");
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
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {ActionName} from non-host uid={SenderUid}, ignoring", false);
            return false;
        }

        if (method.GetCustomAttribute<ClientOnlyReceiveAttribute>() != null && GameSession.IsRoomHost)
            return false;

        if (method.GetCustomAttribute<HostOnlyReceiveAttribute>() != null && !GameSession.IsRoomHost)
        {
            Log.Warning($"{MetaMystia.UI.MultiplayerStatus.RoleTag} {ActionName} received by non-host, ignoring", false);
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

    private string ActionName => GetType().Name;

    private static void LogAction(LogLevel logLevel, string logStr)
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

    protected void LogActionReceived()
    {
        string logStr = $"{MetaMystia.UI.MultiplayerStatus.RoleTag} Received {ActionName}{(OnReceiveLogOnlyAction ? "" : $": {ToLogString()}")}";
        LogAction(OnReceiveLogLevel, logStr);
    }

    protected void LogActionSend()
    {
        string logStr = $"{MetaMystia.UI.MultiplayerStatus.RoleTag} Send {ActionName}{(OnSendLogOnlyAction ? "" : $": {ToLogString()}")}";
        LogAction(OnSendLogLevel, logStr);
    }

    protected void Enqueue()
    {
        if (!GameActions.CanSend(this)) return;
        if (ShouldDiscardOnStory())
        {
            Log.Info($"{MetaMystia.UI.MultiplayerStatus.RoleTag} Will not send (in story): {ActionName}");
            return;
        }
        LogActionSend();
        GameActions.Send(this);
    }

    public static void RegisterAllFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<Action>()) MemoryPackFormatterProvider.Register(new ActionFormatter());
        if (!MemoryPackFormatterProvider.IsRegistered<Action[]>()) MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.ArrayFormatter<Action>());
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

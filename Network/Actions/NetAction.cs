using System;
using System.Reflection;
using BepInEx.Logging;
using MemoryPack;
using SgrYuki;

namespace MetaMystia.Network;

public enum ActionType : ushort
{
    PING,
    PONG,

    HELLO,
    HELLO_ACK,
    REJECT,
    PEER_JOIN,
    PEER_LEAVE,

    SCENE_TRANSIT,
    SYNC,
    READY,
    MESSAGE,
    SELECT,
    CONFIRM_SELECT,
    PREP,
    NIGHTSYNC,
    COOK,
    EXTRACT,
    QTE,
    STORE_FOOD, // 这是往保温箱中存储，仅可以存储 food
    STORE_SELLABLE, // 这是往空位存储，可以存储 sellable（food / beverage）
    EXTRACT_FOOD,
    BUFF,
    IZAKAYA_CLOSE,
    GET_COLLECTABLE, // disabled
    PLAYER_ID_CHANGE,
    SKIN_CHANGE,

    GuestSpawnAction,
    MoveToDeskAction,
    MoveToQueueAction,
    PlayerRepellAction,
    GenerateOrderAction,
    ServeSellableAction,
    EvaluateOrderAction,
    ConfirmServeAction,
    GuestLeaveAction,
    SendFromQueueAction,
    PatientDepletedQueueAction,
    PatientDepletedDeskAction,
    GuestKillAction,
    FundEditAction,
    TipEditAction,
    ExpEditAction,
    PassionEditAction,
    GuestInviteAction,
}

[MemoryPackable]
[MemoryPackUnion((ushort)ActionType.PING, typeof(PingAction))]
[MemoryPackUnion((ushort)ActionType.PONG, typeof(PongAction))]
[MemoryPackUnion((ushort)ActionType.HELLO, typeof(HelloAction))]
[MemoryPackUnion((ushort)ActionType.HELLO_ACK, typeof(HelloAckAction))]
[MemoryPackUnion((ushort)ActionType.REJECT, typeof(RejectAction))]
[MemoryPackUnion((ushort)ActionType.PEER_JOIN, typeof(PeerJoinAction))]
[MemoryPackUnion((ushort)ActionType.PEER_LEAVE, typeof(PeerLeaveAction))]
[MemoryPackUnion((ushort)ActionType.SCENE_TRANSIT, typeof(SceneTransitAction))]
[MemoryPackUnion((ushort)ActionType.SYNC, typeof(SyncAction))]
[MemoryPackUnion((ushort)ActionType.READY, typeof(ReadyAction))]
[MemoryPackUnion((ushort)ActionType.MESSAGE, typeof(MessageAction))]
[MemoryPackUnion((ushort)ActionType.SELECT, typeof(SelectAction))]
[MemoryPackUnion((ushort)ActionType.CONFIRM_SELECT, typeof(ConfirmSelectAction))]
[MemoryPackUnion((ushort)ActionType.PREP, typeof(PrepAction))]
[MemoryPackUnion((ushort)ActionType.NIGHTSYNC, typeof(NightSyncAction))]
[MemoryPackUnion((ushort)ActionType.COOK, typeof(CookAction))]
[MemoryPackUnion((ushort)ActionType.EXTRACT, typeof(ExtractAction))]
[MemoryPackUnion((ushort)ActionType.QTE, typeof(QTEAction))]
[MemoryPackUnion((ushort)ActionType.STORE_FOOD, typeof(StoreFoodAction))]
[MemoryPackUnion((ushort)ActionType.STORE_SELLABLE, typeof(StoreSellableAction))]
[MemoryPackUnion((ushort)ActionType.EXTRACT_FOOD, typeof(ExtractFoodAction))]
[MemoryPackUnion((ushort)ActionType.BUFF, typeof(BuffAction))]
[MemoryPackUnion((ushort)ActionType.IZAKAYA_CLOSE, typeof(IzakayaCloseAction))]
[MemoryPackUnion((ushort)ActionType.GET_COLLECTABLE, typeof(GetCollectableAction))]
[MemoryPackUnion((ushort)ActionType.PLAYER_ID_CHANGE, typeof(PlayerIdChangeAction))]
[MemoryPackUnion((ushort)ActionType.SKIN_CHANGE, typeof(SkinChangeAction))]
[MemoryPackUnion((ushort)ActionType.GuestSpawnAction, typeof(GuestSpawnAction))]
[MemoryPackUnion((ushort)ActionType.MoveToDeskAction, typeof(MoveToDeskAction))]
[MemoryPackUnion((ushort)ActionType.MoveToQueueAction, typeof(MoveToQueueAction))]
[MemoryPackUnion((ushort)ActionType.PlayerRepellAction, typeof(PlayerRepellAction))]
[MemoryPackUnion((ushort)ActionType.GenerateOrderAction, typeof(GenerateOrderAction))]
[MemoryPackUnion((ushort)ActionType.ServeSellableAction, typeof(ServeSellableAction))]
[MemoryPackUnion((ushort)ActionType.EvaluateOrderAction, typeof(EvaluateOrderAction))]
[MemoryPackUnion((ushort)ActionType.ConfirmServeAction, typeof(ConfirmServeAction))]
[MemoryPackUnion((ushort)ActionType.GuestLeaveAction, typeof(GuestLeaveAction))]
[MemoryPackUnion((ushort)ActionType.SendFromQueueAction, typeof(SendFromQueueAction))]
[MemoryPackUnion((ushort)ActionType.PatientDepletedQueueAction, typeof(PatientDepletedQueueAction))]
[MemoryPackUnion((ushort)ActionType.PatientDepletedDeskAction, typeof(PatientDepletedDeskAction))]
[MemoryPackUnion((ushort)ActionType.GuestKillAction, typeof(GuestKillAction))]
[MemoryPackUnion((ushort)ActionType.FundEditAction, typeof(FundEditAction))]
[MemoryPackUnion((ushort)ActionType.TipEditAction, typeof(TipEditAction))]
[MemoryPackUnion((ushort)ActionType.ExpEditAction, typeof(ExpEditAction))]
[MemoryPackUnion((ushort)ActionType.PassionEditAction, typeof(PassionEditAction))]
[MemoryPackUnion((ushort)ActionType.GuestInviteAction, typeof(GuestInviteAction))]
[AutoLog]

public abstract partial class Action
{
    public abstract ActionType Type { get; }
    public long TimestampMs { get; protected set; }
    /// <summary>
    /// 发送者的 UID（主机=0，客机=1,2,3...）
    /// </summary>
    public int SenderUid { get; set; }

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
        TimestampMs = MpManager.TimestampNow;
        SenderUid = PlayerManager.Local.Uid;
    }


    public abstract void OnReceivedDerived();
    public void OnReceived()
    {
        LogActionReceived();
        var targetScene = GetReceivedScene();
        if (targetScene != null && MpManager.LocalScene != targetScene.Value)
        {
            Log.Info($"{MpManager.RoleTag} Received in invalid scene: {Type}: {ToLogString()}");
            return;
        }
        if (ShouldDiscardOnStory())
        {
            Log.Info($"{MpManager.RoleTag} Discarded (in story): {Type}");
            return;
        }
        OnReceivedDerived();
    }

    private Common.UI.Scene? GetReceivedScene()
    {
        var method = this.GetType().GetMethod(nameof(OnReceivedDerived));
        var attr = method.GetCustomAttribute<CheckSceneAttribute>();
        return attr?.Scene;
    }

    private bool ShouldDiscardOnStory()
    {
        if (!MpManager.InStory) return false;
        var method = this.GetType().GetMethod(nameof(OnReceivedDerived));
        return method.GetCustomAttribute<DiscardOnStoryAttribute>() != null;
    }

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
        string logStr = $"{MpManager.RoleTag} Received {Type}{(OnReceiveLogOnlyAction ? "" : $": {ToLogString()}")}";
        LogAction(OnReceiveLogLevel, logStr);
    }

    protected void LogActionSend()
    {
        string logStr = $"{MpManager.RoleTag} Send {Type}{(OnSendLogOnlyAction ? "" : $": {ToLogString()}")}";
        LogAction(OnSendLogLevel, logStr);
    }

    protected void SendToHostOrBroadcast()
    {
        if (!MpManager.IsConnected) return;
        if (ShouldDiscardOnStory())
        {
            Log.Info($"{MpManager.RoleTag} Will not send (in story): {Type}");
            return;
        }

        LogActionSend();

        var packet = NetPacket.FromSingleAction(this);
        MpManager.SendToHostOrBroadcast(packet);
    }

    /// <summary>
    /// 低优先级发送（拥塞时丢弃）
    /// </summary>
    protected void SendToHostOrBroadcastLowPriority()
    {
        if (!MpManager.IsConnected) return;
        if (ShouldDiscardOnStory()) return;

        LogActionSend();

        var packet = NetPacket.FromSingleAction(this);
        MpManager.SendToHostOrBroadcastLowPriority(packet);
    }

    protected void SendToPeer(long peerId)
    {
        if (!MpManager.IsConnected) return;
        if (ShouldDiscardOnStory())
        {
            Log.Info($"{MpManager.RoleTag} Will not send (in story): {Type}");
            return;
        }

        LogActionSend();

        var packet = NetPacket.FromSingleAction(this);
        MpManager.SendToHost(packet);
    }

    /// <summary>
    /// 主机向指定 uid 的客机发送
    /// </summary>
    protected void SendToClient(int uid)
    {
        if (!MpManager.IsHost || !MpManager.IsConnected) return;
        LogActionSend();
        var packet = NetPacket.FromSingleAction(this);
        MpManager.SendToClient(uid, packet);
    }

    /// <summary>
    /// 标记需要主机转发给其他客机的 Action 类型。
    /// 当客机发送一个带有此特性的 Action 时，主机处理后会自动转发给其他所有客机。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class HostRelayAttribute : Attribute { }

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
}

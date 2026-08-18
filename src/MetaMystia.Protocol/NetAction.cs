using System;
using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[MemoryPackUnion((ushort)ActionType.ServerInfoRequest, typeof(ServerInfoRequestAction))]
[MemoryPackUnion((ushort)ActionType.ServerInfoReply, typeof(ServerInfoReplyAction))]
[MemoryPackUnion((ushort)ActionType.Ping, typeof(PingAction))]
[MemoryPackUnion((ushort)ActionType.Pong, typeof(PongAction))]
[MemoryPackUnion((ushort)ActionType.Hello, typeof(HelloAction))]
[MemoryPackUnion((ushort)ActionType.HelloAck, typeof(HelloAckAction))]
[MemoryPackUnion((ushort)ActionType.HandshakeReject, typeof(HandshakeRejectAction))]
[MemoryPackUnion((ushort)ActionType.RoomRequestReject, typeof(RoomRequestRejectAction))]
[MemoryPackUnion((ushort)ActionType.RoomAssign, typeof(RoomAssignAction))]
[MemoryPackUnion((ushort)ActionType.JoinRoomRequest, typeof(JoinRoomRequestAction))]
[MemoryPackUnion((ushort)ActionType.CreateRoomRequest, typeof(CreateRoomRequestAction))]
[MemoryPackUnion((ushort)ActionType.LeaveRoom, typeof(LeaveRoomAction))]
[MemoryPackUnion((ushort)ActionType.LeaveServer, typeof(LeaveServerAction))]
[MemoryPackUnion((ushort)ActionType.RoomKick, typeof(RoomKickAction))]
[MemoryPackUnion((ushort)ActionType.ServerKick, typeof(ServerKickAction))]
[MemoryPackUnion((ushort)ActionType.ServerShutdown, typeof(ServerShutdownAction))]
[MemoryPackUnion((ushort)ActionType.PublicPlayerUpsert, typeof(PublicPlayerUpsertAction))]
[MemoryPackUnion((ushort)ActionType.RoomNewPlayerJoined, typeof(RoomNewPlayerJoinedAction))]
[MemoryPackUnion((ushort)ActionType.RoomMemberLeave, typeof(RoomMemberLeaveAction))]
[MemoryPackUnion((ushort)ActionType.PeerLeave, typeof(PeerLeaveAction))]
[MemoryPackUnion((ushort)ActionType.PlayerChangeId, typeof(PlayerChangeIdAction))]
[MemoryPackUnion((ushort)ActionType.PlayerChangeSkin, typeof(PlayerChangeSkinAction))]
[MemoryPackUnion((ushort)ActionType.Message, typeof(MessageAction))]
[MemoryPackUnion((ushort)ActionType.SceneTransit, typeof(SceneTransitAction))]
[MemoryPackUnion((ushort)ActionType.DayMoveSync, typeof(DayMoveSyncAction))]
[MemoryPackUnion((ushort)ActionType.NightMoveSync, typeof(NightMoveSyncAction))]
[MemoryPackUnion((ushort)ActionType.DayReady, typeof(DayReadyAction))]
[MemoryPackUnion((ushort)ActionType.DayAllReady, typeof(DayAllReadyAction))]
[MemoryPackUnion((ushort)ActionType.SelectIzakaya, typeof(SelectIzakayaAction))]
[MemoryPackUnion((ushort)ActionType.ConfirmIzakaya, typeof(ConfirmIzakayaAction))]
[MemoryPackUnion((ushort)ActionType.UpdatePrep, typeof(UpdatePrepAction))]
[MemoryPackUnion((ushort)ActionType.PrepReady, typeof(PrepReadyAction))]
[MemoryPackUnion((ushort)ActionType.PrepAllReady, typeof(PrepAllReadyAction))]
[MemoryPackUnion((ushort)ActionType.NightCook, typeof(NightCookAction))]
[MemoryPackUnion((ushort)ActionType.ExtractFromCooker, typeof(ExtractFromCookerAction))]
[MemoryPackUnion((ushort)ActionType.StoreFood, typeof(StoreFoodAction))]
[MemoryPackUnion((ushort)ActionType.StoreSellable, typeof(StoreSellableAction))]
[MemoryPackUnion((ushort)ActionType.ExtractFood, typeof(ExtractFoodAction))]
[MemoryPackUnion((ushort)ActionType.QTE, typeof(QTEAction))]
[MemoryPackUnion((ushort)ActionType.Buff, typeof(BuffAction))]
[MemoryPackUnion((ushort)ActionType.GuestInvite, typeof(GuestInviteAction))]
[MemoryPackUnion((ushort)ActionType.GuestSpawn, typeof(GuestSpawnAction))]
[MemoryPackUnion((ushort)ActionType.MoveToDesk, typeof(MoveToDeskAction))]
[MemoryPackUnion((ushort)ActionType.MoveToQueue, typeof(MoveToQueueAction))]
[MemoryPackUnion((ushort)ActionType.PlayerRepell, typeof(PlayerRepellAction))]
[MemoryPackUnion((ushort)ActionType.GenerateOrder, typeof(GenerateOrderAction))]
[MemoryPackUnion((ushort)ActionType.ServeSellable, typeof(ServeSellableAction))]
[MemoryPackUnion((ushort)ActionType.EvaluateOrder, typeof(EvaluateOrderAction))]
[MemoryPackUnion((ushort)ActionType.ConfirmServe, typeof(ConfirmServeAction))]
[MemoryPackUnion((ushort)ActionType.GuestLeave, typeof(GuestLeaveAction))]
[MemoryPackUnion((ushort)ActionType.SendFromQueue, typeof(SendFromQueueAction))]
[MemoryPackUnion((ushort)ActionType.PatientDepletedQueue, typeof(PatientDepletedQueueAction))]
[MemoryPackUnion((ushort)ActionType.PatientDepletedDesk, typeof(PatientDepletedDeskAction))]
[MemoryPackUnion((ushort)ActionType.GuestKill, typeof(GuestKillAction))]
[MemoryPackUnion((ushort)ActionType.FundEdit, typeof(FundEditAction))]
[MemoryPackUnion((ushort)ActionType.TipEdit, typeof(TipEditAction))]
[MemoryPackUnion((ushort)ActionType.ExpEdit, typeof(ExpEditAction))]
[MemoryPackUnion((ushort)ActionType.PassionEdit, typeof(PassionEditAction))]
[MemoryPackUnion((ushort)ActionType.IzakayaClose, typeof(IzakayaCloseAction))]
public abstract partial class NetAction
{
    protected long TimestampMs { get; set; }

    /// <summary>
    /// 发送者的 UID（由主机在转发/入站时写入；HostUid 来自 HelloAck，非固定值）。
    /// </summary>
    public int SenderUid { get; set; }

    [MemoryPackIgnore] public int? WireTargetUid { get; set; }
    [MemoryPackIgnore] public int? WireExceptUid { get; set; }

    /// <summary>
    /// 构造时填充 SenderUid 的来源。mod 启动时覆盖为本地玩家 UID；
    /// 独立服务端/MockClient 保持纯托管默认值，避免触发 Unity/il2cpp 静态初始化。
    /// </summary>
    [MemoryPackIgnore]
    public static Func<int> LocalUidProvider { get; set; } = static () => MpConstants.UnassignedUid;

    [MemoryPackIgnore]
    public static Func<long> TimeProvider { get; set; } = static () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    protected NetAction()
    {
        TimestampMs = TimeProvider();
        SenderUid = LocalUidProvider();
    }

    /// <summary>
    /// 标记需要房主/转发服务器转发给房间内其他玩家的 Action 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RoomRelayAttribute : Attribute { }

    /// <summary>Relay 公域转发标记；直连时与 <see cref="RoomRelayAttribute"/> 相同，由主机转发。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class PublicRelayAttribute : Attribute { }

    public static void RegisterAllFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<NetAction>())
            MemoryPackFormatterProvider.Register(new NetActionFormatter());
        if (!MemoryPackFormatterProvider.IsRegistered<NetAction[]>())
            MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.ArrayFormatter<NetAction>());
    }
}

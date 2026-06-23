namespace MetaMystia.Network;

public enum ActionType : ushort
{
    ServerInfoRequest,
    ServerInfoReply,
    Ping,
    Pong,

    Hello,
    HelloAck,
    Reject,
    PeerLeave,
    PlayerChangeId,
    PlayerChangeSkin,
    Message,

    SceneTransit,
    MoveSync,
    NightMoveSync,

    DayReady,
    DayAllReady,
    SelectIzakaya,
    ConfirmIzakaya,
    UpdatePrep,
    PrepReady,
    PrepAllReady,

    NightCook,
    ExtractFromCooker,
    StoreFood, // 这是往保温箱中存储，仅可以存储 food
    StoreSellable, // 这是往空位存储，可以存储 sellable（food / beverage）
    ExtractFood,
    QTE,
    Buff,

    GuestInvite,
    GuestSpawn,
    MoveToDesk,
    MoveToQueue,
    PlayerRepell,
    GenerateOrder,
    ServeSellable,
    EvaluateOrder,
    ConfirmServe,
    GuestLeave,
    SendFromQueue,
    PatientDepletedQueue,
    PatientDepletedDesk,
    GuestKill,

    FundEdit,
    TipEdit,
    ExpEdit,
    PassionEdit,

    IzakayaClose,

    RoomAssign,
    JoinRoomRequest,
    LeaveRoom,
    RoomKick,
    PlayerPresence,
    CreateRoomRequest,
}

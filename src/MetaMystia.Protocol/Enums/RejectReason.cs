namespace MetaMystia.Network;

public enum RejectReason : ushort
{
    Unknown = 0,
    ModVersionMismatch,
    GameVersionMismatch,
    GameResourcesNotLoaded,
    PrepWorkReconnectBlocked,
    RoomFull,
    RoomIdExhausted,
    InvalidPlayerId,
    DuplicatePeerId,
    UnsupportedServerMode,
    RoomRequestUnsupported,
    RoomNotFound,
    KickedFromRoom,
    KickedFromServer,
    ServerClosed,
}

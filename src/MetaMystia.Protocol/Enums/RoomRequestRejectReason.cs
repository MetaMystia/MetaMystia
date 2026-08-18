namespace MetaMystia.Network;

public enum RoomRequestRejectReason : ushort
{
    Unknown,
    RoomRequestUnsupported,
    RoomNotFound,
    RoomFull,
    RoomIdExhausted,
    InvalidState,
}

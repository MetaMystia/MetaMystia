using MemoryPack;

namespace MetaMystia.Network;

public enum NetworkErrorCode : ushort
{
    None = 0,
    ProtocolMismatch = 1,
    GameMismatch = 2,
    ModMismatch = 3,
    ServerFull = 4,
    RoomFull = 5,
    JoinClosed = 6,
    PlayerNotAvailable = 7,
    HostPreparing = 8,
    RoomMissing = 9,
    RoomEnded = 10,
    StaleRoom = 11,
    HostOnly = 12,
    InvalidLimit = 13,
    DuplicateName = 14,
    WorldDataBudgetExceeded = 15,
    ResourcesNotReadyOrInvalid = 16,
    InvalidHello = 17,
    ReceiveTimeout = 18,
    HandshakeTimeout = 19,
    JoinTimeout = 20,
    ManagementUnconfirmed = 21,
    CancelUnconfirmed = 22,
    LeaveUnconfirmed = 23,
    SendQueueFull = 24,
    ReceiveQueueFull = 25,
    ServerQueueFull = 26,
    TooManyRequests = 27,
    ConnectionLost = 28,
    ConnectFailed = 29,
    AddressUnavailable = 30,
    Disconnected = 31,
    ServerStopped = 32,
    LeftDefaultRoom = 33,
    Finished = 34,
    InvalidMessage = 35,
    AlreadyInRoom = 36,
    DefaultRoomOnly = 37,
    PlayerMissing = 38,
    UidExhausted = 39,
    SendFailed = 40,
    LeftDuringJoin = 41,
    InvalidServerMessage = 42,
}

/// <summary>协议错误及可选上下文；版本指拒绝连接的一端。</summary>
[MemoryPackable]
public sealed partial record NetworkError
{
    public NetworkErrorCode Code { get; init; }
    public int? Count { get; init; }
    public int? Limit { get; init; }
    public int? ProtocolVersion { get; init; }
    public string? GameVersion { get; init; }
    public string? ModVersion { get; init; }

    public static implicit operator NetworkError(NetworkErrorCode code) => new() { Code = code };
    public override string ToString() => Code.ToString();
}

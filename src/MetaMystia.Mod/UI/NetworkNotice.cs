using System;


using MetaMystia.Network;

namespace MetaMystia.UI;

public static class NetworkNotice
{
    public static string Describe(Exception error) => error is NetworkException network ? Describe(network.Code)
        : error is TimeoutException or OperationCanceledException ? TextId.NetworkTimeout.Get()
        : TextId.NetworkFailure.Get();

    public static string Describe(string code) => (code switch
    {
        "ProtocolMismatch" => TextId.NetworkProtocolMismatch,
        "GameMismatch" => TextId.GameVersionMismatch,
        "ModMismatch" => TextId.ModVersionMismatch,
        "ServerFull" => TextId.NetworkServerFull,
        "RoomFull" => TextId.NetworkRoomFull,
        "JoinClosed" => TextId.NetworkJoinUnavailable,
        "PlayerNotAvailable" => TextId.NetworkPlayerUnavailable,
        "HostPreparing" => TextId.NetworkPreparing,
        "RoomMissing" or "RoomEnded" or "StaleRoom" => TextId.NetworkRoomEnded,
        "HostOnly" => TextId.MpKickHostOnly,
        "InvalidLimit" => TextId.NetworkLimit,
        "DuplicateName" => TextId.NetworkDuplicateName,
        "WorldDataBudgetExceeded" or "ResourcesNotReadyOrInvalid" or "InvalidHello" => TextId.NetworkResources,
        "ReceiveTimeout" or "HandshakeTimeout" or "JoinTimeout" or "ManagementUnconfirmed" or "CancelUnconfirmed" or "LeaveUnconfirmed" => TextId.NetworkTimeout,
        "SendQueueFull" or "ReceiveQueueFull" or "ServerQueueFull" or "TooManyRequests" => TextId.NetworkOverload,
        "ConnectionLost" or "ConnectFailed" or "AddressUnavailable" => TextId.NetworkLost,
        "Disconnected" or "ServerStopped" or "LeftDefaultRoom" or "Finished" => TextId.MultiplayerDisconnected,
        _ => TextId.NetworkFailure
    }).Get();
}

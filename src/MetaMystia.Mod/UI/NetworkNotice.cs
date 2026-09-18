using System;


using MetaMystia.Network;

namespace MetaMystia.UI;

public static class NetworkNotice
{
    public static string Describe(Exception error) => error is NetworkException network ? Describe(network.Error)
        : error is TimeoutException or OperationCanceledException ? TextId.NetworkTimeout.Get()
        : TextId.NetworkFailure.Get();

    public static string Describe(NetworkError error)
    {
        var text = Label(error.Code).Get();
        if (error.Code is NetworkErrorCode.ProtocolMismatch or NetworkErrorCode.GameMismatch or NetworkErrorCode.ModMismatch)
            return text + " " + TextId.NetworkVersionDetails.Get(
                Plugin.GameVersion, Plugin.ModVersion, Versions.Current.Protocol,
                error.GameVersion ?? "?", error.ModVersion ?? "?", error.ProtocolVersion?.ToString() ?? "?");
        if ((error.Code is NetworkErrorCode.ServerFull or NetworkErrorCode.RoomFull)
            && error.Count is int count && error.Limit is int limit)
            return text + $" ({count}/{limit})";
        return text;
    }

    private static TextId Label(NetworkErrorCode code) => code switch
    {
        NetworkErrorCode.ProtocolMismatch => TextId.NetworkProtocolMismatch,
        NetworkErrorCode.GameMismatch => TextId.GameVersionMismatch,
        NetworkErrorCode.ModMismatch => TextId.ModVersionMismatch,
        NetworkErrorCode.ServerFull => TextId.NetworkServerFull,
        NetworkErrorCode.RoomFull => TextId.NetworkRoomFull,
        NetworkErrorCode.JoinClosed => TextId.NetworkJoinUnavailable,
        NetworkErrorCode.PlayerNotAvailable => TextId.NetworkPlayerUnavailable,
        NetworkErrorCode.HostPreparing => TextId.NetworkPreparing,
        NetworkErrorCode.RoomMissing or NetworkErrorCode.RoomEnded or NetworkErrorCode.StaleRoom => TextId.NetworkRoomEnded,
        NetworkErrorCode.HostOnly => TextId.MpKickHostOnly,
        NetworkErrorCode.InvalidLimit => TextId.NetworkLimit,
        NetworkErrorCode.DuplicateName => TextId.NetworkDuplicateName,
        NetworkErrorCode.WorldDataBudgetExceeded or NetworkErrorCode.ResourcesNotReadyOrInvalid or NetworkErrorCode.InvalidHello => TextId.NetworkResources,
        NetworkErrorCode.ReceiveTimeout or NetworkErrorCode.HandshakeTimeout or NetworkErrorCode.JoinTimeout or NetworkErrorCode.ManagementUnconfirmed or NetworkErrorCode.CancelUnconfirmed or NetworkErrorCode.LeaveUnconfirmed => TextId.NetworkTimeout,
        NetworkErrorCode.SendQueueFull or NetworkErrorCode.ReceiveQueueFull or NetworkErrorCode.ServerQueueFull or NetworkErrorCode.TooManyRequests => TextId.NetworkOverload,
        NetworkErrorCode.ConnectionLost or NetworkErrorCode.ConnectFailed or NetworkErrorCode.AddressUnavailable => TextId.NetworkLost,
        NetworkErrorCode.Disconnected or NetworkErrorCode.ServerStopped or NetworkErrorCode.LeftDefaultRoom or NetworkErrorCode.Finished => TextId.MultiplayerDisconnected,
        _ => TextId.NetworkFailure
    };
}

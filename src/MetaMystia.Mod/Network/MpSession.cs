namespace MetaMystia.Network;

public enum TransportKind
{
    None,
    DirectHost,
    DirectClient,
    RelayClient
}

public sealed class MpSession
{
    public TransportKind TransportKind { get; private set; } = TransportKind.None;
    public bool IsConnecting { get; private set; }
    public int HostUid { get; private set; } = MpConstants.UnassignedUid;
    public bool RoomRequestPending { get; private set; }

    public bool IsOnline => TransportKind != TransportKind.None && !IsConnecting;
    public bool IsRelay => TransportKind == TransportKind.RelayClient;

    public static string FormatRoomId(ushort roomId) => $"{roomId:X4}";

    public void Reset()
    {
        TransportKind = TransportKind.None;
        IsConnecting = false;
        HostUid = MpConstants.UnassignedUid;
        RoomRequestPending = false;
    }

    public void BeginConnecting(TransportKind transportKind)
    {
        TransportKind = transportKind;
        IsConnecting = true;
        HostUid = MpConstants.UnassignedUid;
        RoomRequestPending = false;
    }

    public void EnterDirectHostRoom()
    {
        TransportKind = TransportKind.DirectHost;
        IsConnecting = false;
        HostUid = MpConstants.HostUid;
    }

    public void EnterDirectClientRoom()
    {
        TransportKind = TransportKind.DirectClient;
        IsConnecting = false;
        // HostUid 由 HelloAck 下发；握手前保持 UnassignedUid。
        HostUid = MpConstants.UnassignedUid;
    }

    /// <summary>客机收到 HelloAck 后记录主机下发的真实 HostUid。</summary>
    public void AssignHostUid(int hostUid) => HostUid = hostUid;

    /// <summary>已连上中继、处于公域；不在任何玩法房间内。</summary>
    public void EnterRelayPublic()
    {
        TransportKind = TransportKind.RelayClient;
        IsConnecting = false;
        HostUid = MpConstants.UnassignedUid;
    }

    public void EnterRelayRoom(int hostUid)
    {
        TransportKind = TransportKind.RelayClient;
        IsConnecting = false;
        HostUid = hostUid;
    }

    public void LeaveRelayRoomToPublic()
    {
        if (TransportKind != TransportKind.RelayClient)
        {
            Reset();
            return;
        }
        EnterRelayPublic();
    }

    public bool TryBeginRoomRequest()
    {
        if (RoomRequestPending) return false;
        RoomRequestPending = true;
        return true;
    }

    public void EndRoomRequest() => RoomRequestPending = false;
}

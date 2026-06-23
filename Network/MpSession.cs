namespace MetaMystia.Network;

public enum TransportKind
{
    None,
    DirectHost,
    DirectClient,
    RelayClient
}

/// <summary>同步域；Relay 可按域转发，后续可扩展更多等级。</summary>
public enum SyncScope
{
    None,
    /// <summary>中继公域（大厅/跨房间可见，非某一局房间）。</summary>
    Public,
    /// <summary>房间内玩法同步。</summary>
    Room
}

/// <summary>仅在 <see cref="SyncScope.Room"/> 下有意义；公域应为 <see cref="None"/>。</summary>
public enum RoomRole
{
    None,
    Host,
    Client
}

public sealed class MpSession
{
    public const ushort DirectRoomId = MpConstants.DirectRoomId;
    public const ushort PublicRoomId = MpConstants.PublicRoomId;

    public TransportKind TransportKind { get; private set; } = TransportKind.None;
    public SyncScope SyncScope { get; private set; } = SyncScope.None;
    public RoomRole RoomRole { get; private set; } = RoomRole.None;
    public bool IsConnecting { get; private set; }
    public ushort RoomId { get; private set; } = PublicRoomId;
    public int HostUid { get; private set; } = MpConstants.UnassignedUid;

    public bool IsOnline => TransportKind != TransportKind.None && !IsConnecting;
    public bool IsInPublicScope => SyncScope == SyncScope.Public;
    public bool IsInRoom => SyncScope == SyncScope.Room;
    public bool IsRoomHost => IsInRoom && RoomRole == RoomRole.Host;
    public bool IsRoomClient => IsInRoom && RoomRole == RoomRole.Client;
    public bool IsRelay => TransportKind == TransportKind.RelayClient;
    public string RoomIdHex => FormatRoomId(RoomId);

    public static string FormatRoomId(ushort roomId) => $"{roomId:X4}";

    public void Reset()
    {
        TransportKind = TransportKind.None;
        SyncScope = SyncScope.None;
        RoomRole = RoomRole.None;
        IsConnecting = false;
        RoomId = PublicRoomId;
        HostUid = MpConstants.UnassignedUid;
    }

    public void BeginConnecting(TransportKind transportKind)
    {
        TransportKind = transportKind;
        SyncScope = SyncScope.None;
        RoomRole = RoomRole.None;
        IsConnecting = true;
        RoomId = PublicRoomId;
        HostUid = MpConstants.UnassignedUid;
    }

    public void EnterDirectHostRoom()
    {
        TransportKind = TransportKind.DirectHost;
        SyncScope = SyncScope.Room;
        RoomRole = RoomRole.Host;
        IsConnecting = false;
        RoomId = DirectRoomId;
        HostUid = MpConstants.HostUid;
    }

    public void EnterDirectClientRoom()
    {
        TransportKind = TransportKind.DirectClient;
        SyncScope = SyncScope.Room;
        RoomRole = RoomRole.Client;
        IsConnecting = false;
        RoomId = DirectRoomId;
        // HostUid 由 HelloAck 下发；握手前保持 UnassignedUid。
        HostUid = MpConstants.UnassignedUid;
    }

    /// <summary>客机收到 HelloAck 后记录主机下发的真实 HostUid。</summary>
    public void AssignHostUid(int hostUid) => HostUid = hostUid;

    /// <summary>已连上中继、处于公域；不在任何玩法房间内。</summary>
    public void EnterRelayPublic()
    {
        TransportKind = TransportKind.RelayClient;
        SyncScope = SyncScope.Public;
        RoomRole = RoomRole.None;
        IsConnecting = false;
        RoomId = PublicRoomId;
        HostUid = MpConstants.UnassignedUid;
    }

    public void EnterRelayRoom(RoomRole roomRole, ushort roomId, int hostUid)
    {
        TransportKind = TransportKind.RelayClient;
        SyncScope = SyncScope.Room;
        RoomRole = roomRole;
        IsConnecting = false;
        RoomId = roomId;
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
}

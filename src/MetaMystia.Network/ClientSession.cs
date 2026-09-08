using System.Collections.ObjectModel;

using MetaMystia.Protocol;

namespace MetaMystia.Network.Core;

public enum ConnectionStage { Offline, Connecting, Handshaking, Online }
public enum SessionEventKind { Online, StateChanged, Payload, RequestCompleted, Disconnected }
public sealed record SessionEvent(SessionEventKind Kind, string Detail = "", Payload? Payload = null, RoomOperation? Operation = null, bool Reconciled = false);
public readonly record struct RoomBinding(Guid RoomId, long MembershipId);
public sealed record PendingRequest(long Id, RoomOperation Operation, long StartedAt, bool Reconciling = false);

public sealed class RoomView
{
    public Guid Id { get; }
    public string Name { get; }
    public int HostUid { get; }
    public int Capacity { get; }
    public bool AdmissionOpen { get; }
    public long Revision { get; }
    public IReadOnlyDictionary<int, RoomMember> Members { get; }

    internal RoomView(RoomSnapshot snapshot)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        HostUid = snapshot.HostUid;
        Capacity = snapshot.Capacity;
        AdmissionOpen = snapshot.AdmissionOpen;
        Revision = snapshot.Revision;
        Members = new ReadOnlyDictionary<int, RoomMember>(snapshot.Members.ToDictionary(p => p.Uid));
    }
}

// 只应用端点确认。请求和确认事实分开保存，所有方法由同一线程调用。
public sealed class ClientSession
{
    public const long RequestTimeoutMs = 5_000;
    private readonly Queue<Message> _outbox = new();
    private readonly Queue<SessionEvent> _events = new();
    private readonly Dictionary<(int, Route, ushort), long> _received = new();
    private long _deadline;
    private long _nextRequest;
    private long _sequence;
    private long _revision = -1;
    private long _nextPing;
    private long _lastReceived;
    private Hello? _hello;

    public long Attempt { get; private set; }
    public ConnectionStage Stage { get; private set; }
    public int SelfUid { get; private set; } = -1;
    public Guid DefaultRoom { get; private set; }
    public RoomView? Room { get; private set; }
    public PendingRequest? Pending { get; private set; }
    public IReadOnlyDictionary<int, PlayerProfile> Players { get; private set; } = new ReadOnlyDictionary<int, PlayerProfile>(new Dictionary<int, PlayerProfile>());
    public IReadOnlyList<RoomSummary> Rooms { get; private set; } = Array.Empty<RoomSummary>();
    public bool IsOnline => Stage == ConnectionStage.Online;
    public bool IsInRoom => IsOnline && Room != null;
    public bool IsRoomHost => IsInRoom && Room!.HostUid == SelfUid;
    public bool IsRoomClient => IsInRoom && !IsRoomHost;
    public int HostUid => Room?.HostUid ?? -1;
    public RoomBinding Binding => Room?.Members.TryGetValue(SelfUid, out var member) == true ? new(Room.Id, member.MembershipId) : default;
    // 更名、改容量等请求超时不撤销当前绑定；主动退房才暂停玩法。
    public bool CanPlay => IsInRoom && Pending?.Operation != RoomOperation.Leave;
    public long LatencyMs { get; private set; }
    public long TimeOffsetMs { get; private set; }

    public long BeginConnect(string name, string application, long now)
    {
        Reset();
        Attempt++;
        Stage = ConnectionStage.Connecting;
        _deadline = now + Endpoint.HandshakeTimeoutMs;
        _hello = new Hello { Name = name, Application = application };
        return Attempt;
    }

    public bool TransportConnected(long attempt, long now)
    {
        if (attempt != Attempt || Stage != ConnectionStage.Connecting) return false;
        Stage = ConnectionStage.Handshaking;
        _deadline = now + Endpoint.HandshakeTimeoutMs;
        _outbox.Enqueue(_hello!);
        return true;
    }

    public void Disconnect(long attempt, string reason = "Disconnected")
    {
        if (attempt != Attempt || Stage == ConnectionStage.Offline) return;
        Reset();
        _events.Enqueue(new(SessionEventKind.Disconnected, reason));
    }

    public void Receive(long attempt, Message message, long now)
    {
        if (attempt != Attempt || Stage is ConnectionStage.Offline or ConnectionStage.Connecting) return;
        if (message is Rejected rejected) { Disconnect(attempt, rejected.Reason); return; }
        _lastReceived = now;
        if (Stage == ConnectionStage.Handshaking)
        {
            if (message is not Welcome welcome || !ValidSnapshot(welcome.State))
            { Disconnect(attempt, "Invalid handshake response"); return; }
            Stage = ConnectionStage.Online;
            SelfUid = welcome.State.SelfUid;
            DefaultRoom = welcome.DefaultRoom;
            Apply(welcome.State);
            _events.Enqueue(new(SessionEventKind.Online));
            _nextPing = now;
            return;
        }

        switch (message)
        {
            case SessionState state:
                if (state.SelfUid != SelfUid || !ValidSnapshot(state)) { Disconnect(attempt, "Invalid session snapshot"); return; }
                Apply(state);
                if (Pending is { } pending && state.RequestId == pending.Id)
                {
                    Pending = null;
                    _events.Enqueue(new(SessionEventKind.RequestCompleted, state.Error, Operation: pending.Operation, Reconciled: pending.Reconciling));
                }
                break;
            case Payload payload:
                if (!AcceptPayload(payload)) return;
                _events.Enqueue(new(SessionEventKind.Payload, Payload: payload));
                break;
            case Pong pong:
                if (pong.SentAt > now) return;
                LatencyMs = now - pong.SentAt;
                TimeOffsetMs = pong.ServerTime + LatencyMs / 2 - now;
                break;
            default: Disconnect(attempt, "Unexpected server message"); break;
        }
    }

    public bool Request(RoomOperation operation, long now, Guid roomId = default, string name = "", int capacity = 4,
        int targetUid = -1, bool admissionOpen = false)
    {
        if (!IsOnline || Pending != null) return false;
        var binding = Binding;
        var request = ++_nextRequest;
        Pending = new(request, operation, now);
        _outbox.Enqueue(new RoomCommand
        {
            RequestId = request, Operation = operation, RoomId = operation == RoomOperation.Join ? roomId : binding.RoomId,
            MembershipId = binding.MembershipId, Name = name, Capacity = capacity, TargetUid = targetUid, AdmissionOpen = admissionOpen
        });
        return true;
    }

    public bool Send(Route route, ushort kind, byte[] data, long phaseId = 0, int targetUid = -1)
    {
        if (!IsOnline || (!Payload.IsPublic(route) && !CanPlay)) return false;
        if (route is Route.HostEvent or Route.HostState && !IsRoomHost) return false;
        var binding = Payload.IsPublic(route) ? default : Binding;
        _outbox.Enqueue(new Payload
        {
            Route = route, Kind = kind, Data = data, PhaseId = phaseId, TargetUid = targetUid, Sequence = ++_sequence,
            SenderUid = SelfUid, RoomId = binding.RoomId, SenderMembershipId = binding.MembershipId
        });
        return true;
    }

    public void Tick(long now)
    {
        if (Stage is ConnectionStage.Connecting or ConnectionStage.Handshaking && now >= _deadline)
        { Disconnect(Attempt, "Connection or handshake timed out"); return; }
        if (!IsOnline) return;
        if (now - _lastReceived >= 30_000) { Disconnect(Attempt, "Server heartbeat timed out"); return; }
        if (Pending is { } pending && now - pending.StartedAt >= RequestTimeoutMs)
        {
            if (pending.Reconciling) { Disconnect(Attempt, "Session reconciliation timed out"); return; }
            Pending = pending with { Id = ++_nextRequest, StartedAt = now, Reconciling = true };
            _outbox.Enqueue(new RoomCommand { RequestId = Pending.Id, Operation = RoomOperation.Query });
        }
        if (now >= _nextPing)
        {
            _nextPing = now + 3_000;
            _outbox.Enqueue(new Ping { SentAt = now });
        }
    }

    public bool TryDequeue(out Message message) => _outbox.TryDequeue(out message!);
    public bool TryDequeueEvent(out SessionEvent change) => _events.TryDequeue(out change!);

    private void Apply(SessionState state)
    {
        if (state.Revision <= _revision) return;
        var oldBinding = Binding;
        _revision = state.Revision;
        Players = new ReadOnlyDictionary<int, PlayerProfile>(state.Players.ToDictionary(p => p.Uid));
        Rooms = Array.AsReadOnly(state.Rooms.ToArray());
        Room = state.Room == null ? null : new(state.Room);
        foreach (var key in _received.Keys.Where(k => !Players.ContainsKey(k.Item1) || (oldBinding != Binding && !Payload.IsPublic(k.Item2))).ToArray())
            _received.Remove(key);
        _events.Enqueue(new(SessionEventKind.StateChanged));
    }

    private bool AcceptPayload(Payload payload)
    {
        if (!Enum.IsDefined(typeof(Route), payload.Route) || !Players.ContainsKey(payload.SenderUid) || payload.Data == null || payload.Sequence <= 0) return false;
        if (payload.TargetUid != -1 && payload.TargetUid != SelfUid) return false;
        if (!Payload.IsPublic(payload.Route))
        {
            if (!CanPlay || payload.RoomId != Binding.RoomId || payload.RecipientMembershipId != Binding.MembershipId) return false;
            if (!Room!.Members.TryGetValue(payload.SenderUid, out var sender) || payload.SenderMembershipId != sender.MembershipId) return false;
            if (payload.Route is Route.HostEvent or Route.HostState && payload.SenderUid != HostUid) return false;
            if (payload.Route == Route.HostRequest && !IsRoomHost) return false;
        }
        var key = (payload.SenderUid, payload.Route, payload.Kind);
        if (_received.TryGetValue(key, out long sequence) && payload.Sequence <= sequence) return false;
        _received[key] = payload.Sequence;
        return true;
    }

    private static bool ValidSnapshot(SessionState state)
    {
        if (state?.Players == null || state.Rooms == null || state.Players.Any(p => p == null) || state.Rooms.Any(r => r == null)
            || state.Players.Select(p => p.Uid).Distinct().Count() != state.Players.Length) return false;
        var ids = state.Players.Select(p => p.Uid).ToHashSet();
        if (state.SelfUid < 0 || !ids.Contains(state.SelfUid)) return false;
        return state.Room is not { } room || (room.Id != Guid.Empty && room.Members != null && room.Members.Length > 0
            && room.Members.All(m => m != null && m.MembershipId > 0 && ids.Contains(m.Uid))
            && room.Members.Select(m => m.Uid).Distinct().Count() == room.Members.Length
            && room.Members.Any(m => m.Uid == state.SelfUid) && room.Members.Any(m => m.Uid == room.HostUid));
    }

    private void Reset()
    {
        Stage = ConnectionStage.Offline;
        SelfUid = -1;
        Room = null;
        DefaultRoom = Guid.Empty;
        Pending = null;
        Players = new ReadOnlyDictionary<int, PlayerProfile>(new Dictionary<int, PlayerProfile>());
        Rooms = Array.Empty<RoomSummary>();
        _outbox.Clear();
        _events.Clear();
        _received.Clear();
        _revision = -1;
        _nextRequest = _sequence = 0;
        LatencyMs = TimeOffsetMs = 0;
    }
}

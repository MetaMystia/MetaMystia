using MetaMystia.Protocol;

namespace MetaMystia.Network.Core;

public sealed record Delivery(long ConnectionId, Message Message, bool Close = false);

// 单线程状态机；套接字与 Unity 都不在这里。只有 Hello 成功才能创建 Player。
public sealed class Endpoint
{
    private sealed class Connection(long openedAt)
    {
        public long OpenedAt { get; } = openedAt;
        public long LastReceived { get; set; } = openedAt;
        public Player? Player { get; set; }
    }

    private sealed class Player(int uid, long connectionId, string name, string application)
    {
        public int Uid { get; } = uid;
        public long ConnectionId { get; } = connectionId;
        public string Name { get; set; } = name;
        public string Application { get; } = application;
        public Room? Room { get; set; }
        public long MembershipId { get; set; }
        public long LastRequest { get; set; }
        public SessionState? LastReply { get; set; }
        public long LastSequence { get; set; }
        public Dictionary<(Route, ushort), Payload> States { get; } = new();
    }

    private sealed class Room(Guid id, string name, Player host, int capacity)
    {
        public Guid Id { get; } = id;
        public string Name { get; } = name;
        public Player Host { get; } = host;
        public int Capacity { get; set; } = capacity;
        public bool AdmissionOpen { get; set; } = true;
        public long Revision { get; set; }
        public Dictionary<int, Player> Members { get; } = new();
    }

    public const long HandshakeTimeoutMs = 10_000;
    private readonly Dictionary<long, Connection> _connections = new();
    private readonly Dictionary<int, Player> _players = new();
    private readonly Dictionary<Guid, Room> _rooms = new();
    private readonly Queue<Delivery> _outbox = new();
    private int _nextUid;
    private long _nextMembership;
    private long _revision;
    public int MaxPlayers { get; }
    public Guid DefaultRoom { get; set; }
    public int OnlineCount => _players.Count;
    public int RoomCount => _rooms.Count;

    public Endpoint(int maxPlayers = 64) => MaxPlayers = maxPlayers;

    public void Accept(long connectionId, long now)
    {
        if (_connections.ContainsKey(connectionId)) throw new InvalidOperationException("Duplicate connection");
        if (_connections.Count >= MaxPlayers * 2)
        {
            _outbox.Enqueue(new(connectionId, new Rejected { Reason = "Server is full" }, true));
            return;
        }
        _connections.Add(connectionId, new(now));
    }

    public void Receive(long connectionId, Message message, long now)
    {
        if (!_connections.TryGetValue(connectionId, out var connection)) return;
        connection.LastReceived = now;
        if (connection.Player == null)
        {
            if (message is not Hello hello) { Reject(connectionId, "Handshake required"); return; }
            if (now - connection.OpenedAt >= HandshakeTimeoutMs) { Reject(connectionId, "Handshake timed out"); return; }
            if (hello.Protocol != ProtocolVersion.Current) { Reject(connectionId, "Protocol version mismatch"); return; }
            if (!ValidName(hello.Name) || string.IsNullOrWhiteSpace(hello.Application) || hello.Application.Length > 200)
            { Reject(connectionId, "Invalid identity"); return; }
            if (_players.Count >= MaxPlayers) { Reject(connectionId, "Server is full"); return; }
            // 直连端点只有一个默认房间；进不去就不放行，避免玩家停在公共域却无法参与玩法。
            if (DefaultRoom != Guid.Empty && _rooms.TryGetValue(DefaultRoom, out var defaultRoom)
                && (!defaultRoom.AdmissionOpen || defaultRoom.Members.Count >= defaultRoom.Capacity))
            { Reject(connectionId, defaultRoom.AdmissionOpen ? "Room is full" : "Room admission is closed"); return; }
            var player = new Player(_nextUid++, connectionId, hello.Name, hello.Application);
            connection.Player = player;
            _players.Add(player.Uid, player);
            if (DefaultRoom != Guid.Empty) Join(player, _rooms[DefaultRoom]);
            _revision++;
            _outbox.Enqueue(new(connectionId, new Welcome { State = Snapshot(player), DefaultRoom = DefaultRoom }));
            BroadcastState(exceptUid: player.Uid);
            ReplayStates(player);
            return;
        }

        var sender = connection.Player;
        switch (message)
        {
            case RoomCommand command: HandleCommand(sender, command); break;
            case Payload payload: RoutePayload(sender, payload); break;
            case Ping ping: _outbox.Enqueue(new(connectionId, new Pong { SentAt = ping.SentAt, ServerTime = now })); break;
            default: Reject(connectionId, "Unexpected message"); break;
        }
    }

    public void Tick(long now)
    {
        foreach (var pair in _connections.Where(p => p.Value.Player == null && now - p.Value.OpenedAt >= HandshakeTimeoutMs).ToArray())
            Reject(pair.Key, "Handshake timed out");
        foreach (var pair in _connections.Where(p => p.Value.Player != null && now - p.Value.LastReceived >= 30_000).ToArray())
            Reject(pair.Key, "Client heartbeat timed out");
    }

    public void Disconnect(long connectionId)
    {
        if (!_connections.Remove(connectionId, out var connection) || connection.Player is not { } player) return;
        Leave(player);
        _players.Remove(player.Uid);
        _revision++;
        BroadcastState();
    }

    public bool TryDequeue(out Delivery delivery) => _outbox.TryDequeue(out delivery!);

    private void Reject(long connectionId, string reason)
    {
        _outbox.Enqueue(new(connectionId, new Rejected { Reason = reason }, true));
        Disconnect(connectionId);
    }

    private void HandleCommand(Player player, RoomCommand command)
    {
        if (command.RequestId <= 0) { Reject(player.ConnectionId, "Invalid request id"); return; }
        if (command.RequestId <= player.LastRequest)
        {
            if (command.RequestId == player.LastRequest && player.LastReply != null)
                _outbox.Enqueue(new(player.ConnectionId, player.LastReply));
            return;
        }

        string error = "";
        bool joined = false;
        var room = player.Room;
        bool bound = room != null && command.RoomId == room.Id && command.MembershipId == player.MembershipId;
        switch (command.Operation)
        {
            case RoomOperation.Create:
                if (room != null) error = "Already in a room";
                else if (!ValidName(command.Name) || command.Capacity < 2 || command.Capacity > MaxPlayers) error = "Invalid room settings";
                else
                {
                    room = new(Guid.NewGuid(), command.Name, player, command.Capacity);
                    _rooms.Add(room.Id, room);
                    Join(player, room);
                    joined = true;
                }
                break;
            case RoomOperation.Join:
                if (room != null) error = "Already in a room";
                else if (!_rooms.TryGetValue(command.RoomId, out var target)) error = "Room no longer exists";
                else if (!target.AdmissionOpen) error = "Room admission is closed";
                else if (target.Members.Count >= target.Capacity) error = "Room is full";
                else if (target.Host.Application != player.Application) error = "Game or Mod version mismatch";
                else { Join(player, target); joined = true; }
                break;
            case RoomOperation.Leave:
                if (!bound) error = "Room binding changed";
                else Leave(player);
                break;
            case RoomOperation.Kick:
                if (!bound || room!.Host != player) error = "Host permission required";
                else if (command.TargetUid == player.Uid || !room.Members.TryGetValue(command.TargetUid, out var kicked)) error = "Invalid member";
                else Leave(kicked);
                break;
            case RoomOperation.SetAdmission:
                if (!bound || room!.Host != player) error = "Host permission required";
                else { room.AdmissionOpen = command.AdmissionOpen; room.Revision++; }
                break;
            case RoomOperation.Rename:
                if (!ValidName(command.Name)) error = "Invalid name";
                else player.Name = command.Name;
                break;
            case RoomOperation.SetCapacity:
                if (!bound || room!.Host != player) error = "Host permission required";
                else if (command.Capacity < Math.Max(2, room.Members.Count) || command.Capacity > MaxPlayers) error = "Invalid room settings";
                else { room.Capacity = command.Capacity; room.Revision++; }
                break;
            case RoomOperation.Query: break;
            default: error = "Unknown operation"; break;
        }

        if (error.Length == 0) _revision++;
        var reply = Snapshot(player, command.RequestId, error);
        player.LastRequest = command.RequestId;
        player.LastReply = reply;
        _outbox.Enqueue(new(player.ConnectionId, reply));
        if (error.Length == 0)
        {
            BroadcastState(exceptUid: player.Uid);
            if (joined) ReplayStates(player);
        }
    }

    // 公共状态对所有人重放，房间状态只对当前绑定重放；客户端按序号丢弃重复块。
    private void ReplayStates(Player player)
    {
        foreach (var other in _players.Values)
            foreach (var state in other.States.Values)
                if (Payload.IsPublic(state.Route) || player.Room?.Members.ContainsKey(other.Uid) == true)
                    DeliverPayload(player, state);
    }

    private void Join(Player player, Room room)
    {
        player.Room = room;
        player.MembershipId = ++_nextMembership;
        room.Members.Add(player.Uid, player);
        room.Revision++;
    }

    private void Leave(Player player)
    {
        if (player.Room is not { } room) return;
        var leaving = room.Host == player ? room.Members.Values.ToArray() : new[] { player };
        foreach (var member in leaving)
        {
            member.Room = null;
            member.MembershipId = 0;
            foreach (var key in member.States.Keys.Where(k => !Payload.IsPublic(k.Item1)).ToArray()) member.States.Remove(key);
            room.Members.Remove(member.Uid);
        }
        room.Revision++;
        if (room.Members.Count == 0) _rooms.Remove(room.Id);
        if (DefaultRoom == room.Id && !_rooms.ContainsKey(room.Id)) DefaultRoom = Guid.Empty;
    }

    private void RoutePayload(Player sender, Payload incoming)
    {
        if (!Enum.IsDefined(typeof(Route), incoming.Route) || incoming.Data == null || incoming.Data.Length > ProtocolVersion.MaxFrameBytes / 2)
        { Reject(sender.ConnectionId, "Invalid payload"); return; }
        if (incoming.Sequence <= sender.LastSequence) return;
        sender.LastSequence = incoming.Sequence;
        var room = sender.Room;
        if (!Payload.IsPublic(incoming.Route))
        {
            if (room == null || incoming.RoomId != room.Id || incoming.SenderMembershipId != sender.MembershipId) return;
            if (incoming.Route is Route.HostEvent or Route.HostState && room.Host != sender) return;
        }
        var payload = CopyPayload(incoming, sender.Uid, Payload.IsPublic(incoming.Route) ? Guid.Empty : room!.Id,
            Payload.IsPublic(incoming.Route) ? 0 : sender.MembershipId, 0);
        if (Payload.IsState(payload.Route))
        {
            if (payload.TargetUid != -1) return;
            var key = (payload.Route, payload.Kind);
            int bytes = sender.States.Where(p => p.Key != key).Sum(p => p.Value.Data.Length) + payload.Data.Length;
            if ((!sender.States.ContainsKey(key) && sender.States.Count >= 32) || bytes > ProtocolVersion.MaxFrameBytes / 2)
            { Reject(sender.ConnectionId, "State cache limit exceeded"); return; }
            sender.States[key] = payload;
        }
        var recipients = Payload.IsPublic(payload.Route) ? _players.Values.AsEnumerable() : room!.Members.Values;
        if (payload.Route == Route.HostRequest) recipients = new[] { room!.Host };
        foreach (var recipient in recipients)
        {
            if (recipient.Uid == sender.Uid && payload.Route != Route.HostRequest) continue;
            if (payload.TargetUid != -1 && payload.TargetUid != recipient.Uid) continue;
            // 载荷版本不同的玩家仍可共享服务器目录，但不解析彼此的 Mod 数据。
            if (recipient.Application == sender.Application) DeliverPayload(recipient, payload);
        }
    }

    private void DeliverPayload(Player recipient, Payload payload)
    {
        if (!_players.TryGetValue(payload.SenderUid, out var sender) || sender.Application != recipient.Application) return;
        _outbox.Enqueue(new(recipient.ConnectionId, CopyPayload(payload, payload.SenderUid, payload.RoomId,
            payload.SenderMembershipId, Payload.IsPublic(payload.Route) ? 0 : recipient.MembershipId)));
    }

    private static Payload CopyPayload(Payload source, int sender, Guid room, long membership, long recipient) => new()
    {
        Route = source.Route, Kind = source.Kind, Sequence = source.Sequence, SenderUid = sender,
        TargetUid = source.TargetUid, RoomId = room, SenderMembershipId = membership,
        RecipientMembershipId = recipient, PhaseId = source.PhaseId, Data = source.Data.ToArray()
    };

    private SessionState Snapshot(Player player, long request = 0, string error = "") => new()
    {
        Revision = _revision, RequestId = request, Error = error, SelfUid = player.Uid,
        Players = _players.Values.Select(p => new PlayerProfile(p.Uid, p.Name)).ToArray(),
        Rooms = _rooms.Values.Select(r => new RoomSummary(r.Id, r.Name, r.Host.Uid, r.Members.Count, r.Capacity, r.AdmissionOpen)).ToArray(),
        Room = player.Room is { } room ? new(room.Id, room.Name, room.Host.Uid, room.Capacity, room.AdmissionOpen,
            room.Revision, room.Members.Values.Select(p => new RoomMember(p.Uid, p.MembershipId)).ToArray()) : null
    };

    private void BroadcastState(int exceptUid = -1)
    {
        foreach (var player in _players.Values)
            if (player.Uid != exceptUid) _outbox.Enqueue(new(player.ConnectionId, Snapshot(player)));
    }

    public static bool ValidName(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 40
        && !value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '<' or '>');
}

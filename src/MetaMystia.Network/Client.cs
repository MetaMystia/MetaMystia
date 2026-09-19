using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

using Common.UI;

namespace MetaMystia.Network;

public sealed class Client : IDisposable
{
    private sealed class Session
    {
        internal Connection? Wire;
        internal readonly Channel<Frame> Incoming = Channel.CreateBounded<Frame>(Protocol.QueueCapacity);
        internal readonly TaskCompletionSource<int> Connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly Dictionary<long, TaskCompletionSource<Control>> Pending = [];
        internal readonly HashSet<long> Cancelled = [];
        internal NetworkError? End;
        internal bool EndNotified, Joining, Leaving;
        internal long SuppressedMembership;
    }
    private readonly object gate = new();
    private Session? session;
    private Snapshot state = new();
    private int uid;
    private long nextRequest;
    private int dispatching;
    public Versions Versions { get; }
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public int Uid { get { lock (gate) return uid; } }
    internal int PendingFrames { get { lock (gate) return session?.Incoming.Reader.Count ?? 0; } }
    public bool IsConnected { get { lock (gate) return uid != 0 && session?.End == null; } }
    public Snapshot State { get { lock (gate) return state.Copy(); } }
    public event Action? StateChanged;
    public event Action<ReceivedMessage>? MessageReceived;
    public event Action<NetworkError>? ConnectionEnded;
    public event Action<Exception>? CallbackError;
    public Client(Versions? versions = null) => Versions = versions ?? Versions.Current;

    public Task ConnectAsync(IPEndPoint endpoint, Player player, CancellationToken cancellationToken = default) => ConnectCore(endpoint, player, "", cancellationToken);

    internal async Task ConnectCore(IPEndPoint endpoint, Player player, string key, CancellationToken token)
    {
        Protocol.Validate(player, true);
        var hello = Protocol.Hello(Versions, player, key);
        var current = new Session();
        lock (gate)
        {
            if (session != null && !session.EndNotified) throw new InvalidOperationException("Dispatch previous connection end first");
            session = current; state = new(); uid = 0;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(OperationTimeout);
        var tcp = new TcpClient(endpoint.AddressFamily);
        try
        {
            await tcp.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token).ConfigureAwait(false);
            lock (gate)
            {
                if (current.End != null) throw new NetworkException(current.End);
                current.Wire = new(tcp, ConnectionTimeout, frame =>
                {
                    if (frame.Kind == Kind.Ping) { current.Wire!.Send(new(Kind.Pong, [])); return; }
                    if (!current.Incoming.Writer.TryWrite(frame)) current.Wire!.Close(NetworkErrorCode.ReceiveQueueFull);
                }, reason => { lock (gate) current.End ??= reason; });
                current.Wire.Send(new(Kind.Hello, hello));
                current.Wire.Start();
            }
            await current.Connected.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch
        {
            tcp.Dispose();
            lock (gate) End(current, NetworkErrorCode.ConnectFailed);
            throw;
        }
    }

    public Task<Room> CreateRoomAsync(int maxPlayers = 2, CancellationToken token = default) => Enter(Command.Create, maxPlayers, 0, token);
    public Task<Room> JoinRoomAsync(long roomId, CancellationToken token = default) => Enter(Command.Join, 0, roomId, token);

    private async Task<Room> Enter(Command command, int value, long roomId, CancellationToken token)
    {
        Session current;
        long request;
        Task<Control> response;
        lock (gate)
        {
            current = Require();
            if (current.Joining || current.Leaving || state.Room != null) throw new InvalidOperationException("Room operation pending or already joined");
            current.Joining = true;
            request = checked(++nextRequest);
            response = Request(current, new() { Command = command, Value = value, Room = roomId, Request = request });
        }
        try
        {
            var result = await response.WaitAsync(OperationTimeout, token).ConfigureAwait(false);
            if (result.Error.Code != NetworkErrorCode.None) throw new NetworkException(result.Error);
            lock (gate)
            {
                if (session != current || current.End != null || state.MembershipRequest != request || state.Room == null) throw new NetworkException(NetworkErrorCode.RoomEnded);
                return Protocol.Read<Room>(Protocol.Pack(state.Room));
            }
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
            Task<Control> cancel;
            lock (gate)
            {
                current.Cancelled.Add(request);
                current.Pending.Remove(request);
                if (session == current && state.MembershipRequest == request) state = state with { Room = null, MembershipRequest = 0 };
                if (session == current) FilterMotion();
                cancel = Request(current, new() { Command = Command.Cancel, Request = checked(++nextRequest), CancelRequest = request });
            }
            try { await cancel.WaitAsync(OperationTimeout).ConfigureAwait(false); }
            catch { lock (gate) End(current, NetworkErrorCode.CancelUnconfirmed); }
            throw;
        }
        finally { lock (gate) current.Joining = false; }
    }

    public Task SetJoinableAsync(bool allowed, CancellationToken token = default) => Change(Command.Joinable, allowed ? 1 : 0, token);
    public Task SetRoomPlayerLimitAsync(int maxPlayers, CancellationToken token = default) => Change(Command.Limit, maxPlayers, token);
    public Task KickAsync(int uid, CancellationToken token = default) => Change(Command.Kick, uid, token);
    private async Task Change(Command command, int value, CancellationToken token)
    {
        Session current;
        Task<Control> response;
        lock (gate)
        {
            current = Require();
            var room = state.Room ?? throw new InvalidOperationException("No room");
            response = Request(current, new() { Command = command, Request = checked(++nextRequest), Value = value, Room = room.Id, Membership = MyMembership() });
        }
        try
        {
            var result = await response.WaitAsync(OperationTimeout, token).ConfigureAwait(false);
            if (result.Error.Code != NetworkErrorCode.None) throw new NetworkException(result.Error);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        { lock (gate) End(current, NetworkErrorCode.ManagementUnconfirmed); throw; }
    }

    private Task<Control> Request(Session current, Control command)
    {
        var pending = new TaskCompletionSource<Control>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (current.End != null) { pending.SetException(new NetworkException(current.End)); return pending.Task; }
        if (current.Pending.Count >= Protocol.QueueCapacity)
        { End(current, NetworkErrorCode.TooManyRequests); pending.SetException(new NetworkException(NetworkErrorCode.TooManyRequests)); return pending.Task; }
        current.Pending.Add(command.Request, pending);
        if (!current.Wire!.Send(new(Kind.Command, Protocol.Pack(command)))) End(current, NetworkErrorCode.SendFailed);
        return pending.Task;
    }

    public void LeaveRoom()
    {
        lock (gate)
        {
            var current = Require();
            if (current.Joining) { End(current, NetworkErrorCode.LeftDuringJoin); return; }
            if (state.Room == null) return;
            long membership = MyMembership();
            var command = new Control { Command = Command.Leave, Request = checked(++nextRequest), Room = state.Room.Id, Membership = membership };
            current.SuppressedMembership = membership;
            current.Leaving = true;
            state = state with { Room = null, MembershipRequest = 0 };
            FilterMotion();
            _ = FinishLeave(current, Request(current, command));
        }
    }

    private async Task FinishLeave(Session current, Task<Control> task)
    {
        try { await task.WaitAsync(OperationTimeout).ConfigureAwait(false); }
        catch { lock (gate) End(current, NetworkErrorCode.LeaveUnconfirmed); }
        finally { lock (gate) current.Leaving = false; }
    }

    public void SendMotion(Motion motion)
    {
        Protocol.Validate(motion);
        lock (gate)
        {
            var current = Require();
            var scene = state.World.First(p => p.Uid == uid).Scene;
            if (scene != Scene.DayScene && (scene != Scene.WorkScene || state.Room == null)) return;
            current.Wire!.Send(new(scene == Scene.DayScene ? Kind.Motion : Kind.RoomMotion, Protocol.Pack(motion), Room: state.Room?.Id ?? 0, Membership: MyMembership()));
            UpdatePlayer(uid, p => p with { Motion = motion, HasMotion = true });
        }
    }
    public void SetProfile(string name, Skin skin, Scene scene, GameStage stage)
    {
        var profile = new Player { Name = name, Skin = skin, Scene = scene, Stage = stage };
        Protocol.Validate(profile, false);
        lock (gate)
        {
            Require().Wire!.Send(new(Kind.Profile, Protocol.Pack(profile)));
            UpdatePlayer(uid, p => p with { Name = name, Skin = skin, Scene = scene, Stage = stage,
                Motion = p.Scene == scene ? p.Motion : new(), HasMotion = p.Scene == scene && p.HasMotion });
            FilterMotion();
        }
    }

    public void SendToWorld(ushort type, byte[] body, long request = 0) => Send(type, body, Route.World, 0, request);
    public void SendToRoom(ushort type, byte[] body, long request = 0) => Send(type, body, Route.Room, 0, request);
    public void SendToHost(ushort type, byte[] body, long request = 0) => Send(type, body, Route.Host, 0, request);
    public void SendToPlayer(int target, ushort type, byte[] body, long request = 0) => Send(type, body, Route.Player, target, request);
    public void SendToServer(ushort type, byte[] body, long request = 0) => Send(type, body, Route.Server, 0, request);
    private void Send(ushort type, byte[] body, Route route, int target, long request)
    {
        lock (gate)
        {
            var current = Require();
            current.Wire!.Send(new(Kind.Data, body, Type: type, Route: route, Target: target,
                Room: state.Room?.Id ?? 0, Membership: MyMembership(), Request: request));
        }
    }
    public void Reply(MessageContext context, ushort type, byte[] body)
    {
        lock (gate)
        {
            var current = Require();
            if (context.Room != 0 && (state.Room?.Id != context.Room || MyMembership() != context.RecipientMembership)) return;
            current.Wire!.Send(new(Kind.Data, body, Type: type, Route: Route.Player, Target: context.Sender,
                Room: context.Room, Membership: MyMembership(), RecipientMembership: context.SenderMembership, Request: context.Request));
        }
    }

    public bool IsCurrent(MessageContext context)
    {
        lock (gate)
            return session?.End == null && uid != 0 && context != null && (context.Room == 0
                || (state.Room?.Id == context.Room && MyMembership() == context.RecipientMembership
                    && state.Room.Members.Any(p => p.Uid == context.Sender && p.Membership == context.SenderMembership)));
    }

    public int DispatchPending(int limit = 128)
    {
        if (Interlocked.Exchange(ref dispatching, 1) != 0) throw new InvalidOperationException("Dispatch is not reentrant");
        int count = 0;
        try
        {
            lock (gate)
            {
                var current = session;
                if (current == null) return 0;
                while (count < limit && current.Incoming.Reader.TryRead(out var frame))
                {
                    count++;
                    try { Apply(current, frame); }
                    catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or MemoryPack.MemoryPackSerializationException)
                    { End(current, NetworkErrorCode.InvalidServerMessage); break; }
                }
                if (current.End != null && !current.Incoming.Reader.TryPeek(out _) && !current.EndNotified)
                {
                    End(current, current.End);
                    current.EndNotified = true;
                    Invoke(() => ConnectionEnded?.Invoke(current.End));
                }
            }
        }
        finally { Volatile.Write(ref dispatching, 0); }
        return count;
    }

    private void Apply(Session current, Frame f)
    {
        switch (f.Kind)
        {
            case Kind.Rejected:
                var error = Protocol.ReadError(f.Body);
                current.Connected.TrySetException(new NetworkException(error)); End(current, error); break;
            case Kind.Welcome:
                uid = f.Sender;
                goto case Kind.Snapshot;
            case Kind.Snapshot:
                var local = f.Kind == Kind.Welcome ? null : state.World.FirstOrDefault(p => p.Uid == uid);
                var snapshot = Protocol.Read<Snapshot>(f.Body);
                var membership = snapshot.Room?.Members.FirstOrDefault(p => p.Uid == uid)?.Membership ?? 0;
                if (current.Cancelled.Contains(snapshot.MembershipRequest) || (membership != 0 && membership == current.SuppressedMembership))
                    snapshot = snapshot with { Room = null, MembershipRequest = 0 };
                if (snapshot.Room is { } nextRoom)
                {
                    var previous = state.Room?.Id == nextRoom.Id && MyMembership() == membership
                        ? state.Room.Members.ToDictionary(p => p.Membership) : new Dictionary<long, Player>();
                    snapshot = snapshot with { Room = nextRoom with { Members = nextRoom.Members.Select(p =>
                        p.Resources != null ? p : p with
                        {
                            Resources = previous.TryGetValue(p.Membership, out var old) && old.Uid == p.Uid
                                ? old.Resources : throw new InvalidDataException("Missing initial room resources")
                        }).ToArray() } };
                }
                state = snapshot;
                // 自己的资料与运动不回送；旧快照只更新服务器拥有的成员关系。
                if (local != null) UpdatePlayer(uid, p => p with { Name = local.Name, Skin = local.Skin, Motion = local.Motion, HasMotion = local.HasMotion, Scene = local.Scene, Stage = local.Stage });
                FilterMotion();
                Invoke(() => StateChanged?.Invoke());
                if (f.Kind == Kind.Welcome) current.Connected.TrySetResult(uid);
                break;
            case Kind.Ack:
                var ack = Protocol.Read<Control>(f.Body);
                if (ack.Command == Command.Cancel) current.Cancelled.Remove(ack.CancelRequest);
                if (current.Pending.Remove(ack.Request, out var pending)) pending.TrySetResult(ack);
                break;
            case Kind.Motion:
            case Kind.RoomMotion:
                var self = state.World.FirstOrDefault(p => p.Uid == uid);
                if (f.Kind == Kind.Motion && self?.Scene != Scene.DayScene) return;
                if (f.Kind == Kind.RoomMotion && (self?.Scene != Scene.WorkScene || state.Room?.Id != f.Room || MyMembership() != f.RecipientMembership)) return;
                var motion = Protocol.Read<Motion>(f.Body);
                UpdatePlayer(f.Sender, p => p with { Motion = motion, HasMotion = true });
                Invoke(() => StateChanged?.Invoke()); break;
            case Kind.Profile:
                var profile = Protocol.Read<Player>(f.Body);
                UpdatePlayer(f.Sender, p => p with { Name = profile.Name, Skin = profile.Skin, Scene = profile.Scene, Stage = profile.Stage });
                Invoke(() => StateChanged?.Invoke()); break;
            case Kind.Data:
                if (f.Room != 0 && (state.Room?.Id != f.Room || MyMembership() != f.RecipientMembership)) return;
                Invoke(() => MessageReceived?.Invoke(new(f.Type, new(f.Sender, f.Room, f.Membership, f.RecipientMembership, f.Request), f.Body))); break;
            default: throw new InvalidDataException();
        }
    }

    private void UpdatePlayer(int playerUid, Func<Player, Player> update)
    {
        state = state with
        {
            World = state.World.Select(p => p.Uid == playerUid ? update(p) : p).ToArray(),
            Room = state.Room is { } room ? room with { Members = room.Members.Select(p => p.Uid == playerUid ? update(p) : p).ToArray() } : null
        };
    }

    private void FilterMotion()
    {
        var scene = state.World.FirstOrDefault(p => p.Uid == uid)?.Scene;
        var room = state.Room;
        Player Filter(Player player) => player.Uid == uid || (player.Scene == scene &&
            (scene == Scene.DayScene || (scene == Scene.WorkScene && room?.Members.Any(p => p.Uid == player.Uid) == true)))
            ? player : player with { Motion = new(), HasMotion = false };
        state = state with
        {
            World = state.World.Select(Filter).ToArray(),
            Room = room is null ? null : room with { Members = room.Members.Select(Filter).ToArray() }
        };
    }
    private long MyMembership() => state.Room?.Members.FirstOrDefault(p => p.Uid == uid)?.Membership ?? 0;
    private Session Require() => session is { End: null } s && uid != 0 ? s : throw new InvalidOperationException("Not connected");
    private void Invoke(Action callback)
    { try { callback(); } catch (Exception e) { try { CallbackError?.Invoke(e); } catch { } } }
    private void End(Session current, NetworkError reason)
    {
        current.End ??= reason;
        current.Wire?.Close(reason);
        current.Connected.TrySetException(new NetworkException(current.End));
        foreach (var task in current.Pending.Values) task.TrySetException(new NetworkException(current.End));
        current.Pending.Clear();
        while (current.Incoming.Reader.TryRead(out _)) { }
        if (session == current) { uid = 0; state = new(); }
    }
    public void Disconnect() { lock (gate) { if (session != null) End(session, NetworkErrorCode.Disconnected); } }
    public void Dispose() => Disconnect();
}

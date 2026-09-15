using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

using Common.UI;

namespace MetaMystia.Network;

public sealed class Server : IAsyncDisposable
{
    private sealed class Peer
    {
        internal Connection Wire = null!;
        internal Player? Player;
        internal long Room, MembershipRequest, LastRequest;
        internal DateTime Accepted = DateTime.UtcNow;
        internal bool Rejected;
        internal string? AdmissionError;
    }
    private readonly ServerOptions options;
    private readonly Dictionary<ushort, MessageRule> rules;
    private readonly Channel<Action> events = Channel.CreateBounded<Action>(1024);
    private readonly HashSet<Peer> peers = [];
    private readonly Dictionary<long, Room> rooms = [];
    private readonly CancellationTokenSource stop = new();
    private readonly TcpListener listener;
    private Task actor = Task.CompletedTask, accept = Task.CompletedTask, clock = Task.CompletedTask;
    private static int lastUid;
    private int maxPlayers;
    private long nextRoom, nextMembership;
    private int started, stopping;
    private int lanHost;
    private long defaultRoom;
    public IPEndPoint Endpoint => (IPEndPoint)listener.LocalEndpoint;
    public event Action<ReceivedMessage>? MessageReceived;
    public event Action<Exception>? CallbackError;

    public Server(ServerOptions options)
    {
        if (options.MaxPlayers < 1 || options.MaxPlayers > 256 || options.Timeout < TimeSpan.FromMilliseconds(200)) throw new ArgumentOutOfRangeException(nameof(options));
        this.options = options;
        maxPlayers = options.MaxPlayers;
        rules = options.Messages.ToDictionary(r => r.Id, r => r with { Routes = r.Routes.ToArray() });
        if (rules.Values.Any(r => r.Id == 0 || r.MaxBytes < 0 || r.MaxBytes > 65536 ||
            (r.RoomScoped && r.Routes.Contains(Route.World)) || (!r.RoomScoped && (r.HostOnly || r.Routes.Any(x => x is Route.Room or Route.Host)))))
            throw new ArgumentException("Invalid message rules", nameof(options));
        listener = new(options.Address, options.Port);
        if (options.Address.Equals(IPAddress.IPv6Any)) listener.Server.DualMode = true;
    }

    public Task StartAsync()
    {
        if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("Server is single-use");
        listener.Start();
        actor = Run(); accept = Accept(); clock = Tick();
        return Task.CompletedTask;
    }

    private async Task Run()
    {
        await foreach (var action in events.Reader.ReadAllAsync().ConfigureAwait(false)) action();
    }

    private async Task Accept()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var tcp = await listener.AcceptTcpClientAsync(stop.Token).ConfigureAwait(false);
                try { await events.Writer.WriteAsync(() => Admit(tcp), stop.Token).ConfigureAwait(false); }
                catch { tcp.Dispose(); throw; }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    private void Admit(TcpClient tcp)
    {
        bool local = IPAddress.IsLoopback(((IPEndPoint)tcp.Client.RemoteEndPoint!).Address);
        var peer = new Peer();
        peer.Wire = new(tcp, options.Timeout,
            f => { if (!events.Writer.TryWrite(() => Receive(peer, f))) peer.Wire.Close("ServerQueueFull"); },
            reason => { _ = Post(() => Remove(peer)); });
        peers.Add(peer);
        int occupied = peers.Count(p => !p.Rejected && p.AdmissionError == null);
        if (options.LanKey != null && lanHost == 0 && !local)
            peer.AdmissionError = "HostPreparing";
        else if (occupied > maxPlayers || (options.LanKey != null && lanHost == 0 && occupied > 1)) peer.AdmissionError = "ServerFull";
        peer.Wire.Start();
    }

    private async Task Post(Action action)
    {
        try { await events.Writer.WriteAsync(action).ConfigureAwait(false); }
        catch (ChannelClosedException) { }
    }

    private async Task Tick()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(100, options.Timeout.TotalMilliseconds / 3)));
            while (await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false))
                await events.Writer.WriteAsync(() =>
                {
                    foreach (var p in peers.ToArray())
                    {
                        if (p.Player == null && DateTime.UtcNow - p.Accepted > options.Timeout) p.Wire.Close("HandshakeTimeout");
                        else if (options.LanKey != null && p.Player?.Uid != lanHost && p.Room == 0 && DateTime.UtcNow - p.Accepted > options.Timeout) p.Wire.Close("JoinTimeout");
                        else if (p.Player != null) p.Wire.Send(new(Kind.Ping, []));
                    }
                }, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private void Reject(Peer p, string error)
    { p.Rejected = true; p.Wire.Send(new(Kind.Rejected, Encoding.UTF8.GetBytes(error))); p.Wire.Finish(); }

    private void Receive(Peer p, Frame f)
    {
        if (!peers.Contains(p) || p.Rejected) return;
        try
        {
            if (p.Player == null)
            {
                if (f.Kind != Kind.Hello) throw new InvalidDataException();
                // 先读完 Hello 再拒绝，避免带未读数据关闭 TCP 产生 RST 丢失错误包。
                if (p.AdmissionError != null) { Reject(p, p.AdmissionError); return; }
                var (player, key) = Protocol.ReadHello(f.Body, options.Versions);
                Protocol.Validate(player, true);
                if (peers.Any(x => x.Player != null && string.Equals(x.Player.Name, player.Name, StringComparison.OrdinalIgnoreCase))) { Reject(p, "DuplicateName"); return; }
                if (peers.Count(x => x.Player != null) >= maxPlayers) { Reject(p, "ServerFull"); return; }
                if (options.LanKey != null && lanHost == 0 && key != options.LanKey) { Reject(p, "HostPreparing"); return; }
                if (!CanStore(p, player)) { Reject(p, "WorldDataBudgetExceeded"); return; }
                var newUid = AllocateUid();
                if (newUid == 0) { Reject(p, "UidExhausted"); return; }
                p.Player = player with { Uid = newUid, Membership = 0, HasMotion = false, Motion = new() };
                if (options.LanKey != null && lanHost == 0) lanHost = newUid;
                p.Wire.Send(new(Kind.Welcome, Protocol.Pack(Capture(p)), newUid));
                Publish(p);
                return;
            }
            switch (f.Kind)
            {
                case Kind.Pong: break;
                case Kind.Command: Manage(p, Protocol.Read<Control>(f.Body)); break;
                case Kind.Motion:
                case Kind.RoomMotion:
                    var motion = Protocol.Read<Motion>(f.Body); Protocol.Validate(motion);
                    if (f.Kind == Kind.Motion && p.Player.Scene != Scene.DayScene) break;
                    if (f.Kind == Kind.RoomMotion && (p.Player.Scene != Scene.WorkScene || p.Room == 0 || f.Room != p.Room || f.Membership != p.Player.Membership)) break;
                    p.Player = p.Player with { Motion = motion, HasMotion = true };
                    foreach (var target in peers.Where(x => x != p && x.Player != null && CanSeeMotion(p, x)))
                        target.Wire.Send(new(f.Kind, f.Body, p.Player.Uid, Room: p.Room, Membership: p.Player.Membership, RecipientMembership: target.Player!.Membership));
                    break;
                case Kind.Profile:
                    var profile = Protocol.Read<Player>(f.Body); Protocol.Validate(profile, false);
                    if (peers.Any(x => x != p && x.Player != null && string.Equals(x.Player.Name, profile.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("DuplicateName");
                    bool changedScene = p.Player.Scene != profile.Scene;
                    var updated = p.Player with { Name = profile.Name, Skin = profile.Skin, Scene = profile.Scene };
                    if (changedScene) updated = updated with { Motion = new(), HasMotion = false };
                    if (!CanStore(p, updated)) throw new InvalidDataException("WorldDataBudgetExceeded");
                    p.Player = updated;
                    if (changedScene) Publish();
                    else Broadcast(new(Kind.Profile, Protocol.Pack(p.Player with { Resources = null, Motion = new(), HasMotion = false }), p.Player.Uid), p);
                    break;
                case Kind.Data: RouteMessage(p, f); break;
                default: throw new InvalidDataException("Unexpected message");
            }
        }
        catch (NetworkException e) { Reject(p, e.Code); }
        catch (Exception)
        { if (p.Player == null) Reject(p, "InvalidHello"); else p.Wire.Close("InvalidMessage"); }
    }

    private static int AllocateUid()
    {
        while (true)
        {
            int previous = Volatile.Read(ref lastUid);
            if (previous == int.MaxValue) return 0;
            if (Interlocked.CompareExchange(ref lastUid, previous + 1, previous) == previous) return previous + 1;
        }
    }

    // 世界资料与最坏情况下的完整房间资料各预留一份，另留 32 KiB 给房间摘要和外壳。
    private bool CanStore(Peer owner, Player candidate) =>
        peers.Where(p => p != owner && p.Player != null).Sum(p => 2 * Protocol.Pack(p.Player).Length + 64)
        + 2 * Protocol.Pack(candidate).Length + 64 <= Protocol.MaxFrame - 32768;

    private void Manage(Peer p, Control c)
    {
        if (c.Request <= p.LastRequest) throw new InvalidDataException();
        p.LastRequest = c.Request;
        string error = "";
        var room = rooms.GetValueOrDefault(p.Room);
        bool context = room != null && c.Room == p.Room && c.Membership == p.Player!.Membership;
        switch (c.Command)
        {
            case Command.Create:
                if (p.Room != 0) { error = "AlreadyInRoom"; break; }
                if (options.LanKey != null && p.Player!.Uid != lanHost) { error = "DefaultRoomOnly"; break; }
                if (c.Value < 1 || c.Value > maxPlayers) { error = "InvalidLimit"; break; }
                var created = new Room { Id = checked(++nextRoom), Host = p.Player!.Uid, MaxPlayers = options.LanKey == null ? c.Value : maxPlayers };
                rooms.Add(created.Id, created);
                Join(p, created, c.Request);
                if (options.LanKey != null) defaultRoom = created.Id;
                break;
            case Command.Join:
                if (p.Room != 0) { error = "AlreadyInRoom"; break; }
                var wanted = options.LanKey != null ? defaultRoom : c.Room;
                if (!rooms.TryGetValue(wanted, out var target)) { error = "RoomMissing"; break; }
                if (!target.Joinable) { error = "JoinClosed"; break; }
                if (peers.Count(x => x.Player != null && x.Room == wanted) >= target.MaxPlayers) { error = "RoomFull"; break; }
                Join(p, target, c.Request); break;
            case Command.Cancel:
                if (p.MembershipRequest == c.CancelRequest) Leave(p);
                break;
            case Command.Leave:
                if (context) Leave(p);
                break;
            case Command.Joinable:
            case Command.Limit:
            case Command.Kick:
                if (!context) { error = "StaleRoom"; break; }
                if (room!.Host != p.Player!.Uid) { error = "HostOnly"; break; }
                if (c.Command == Command.Kick)
                {
                    var targetPeer = peers.FirstOrDefault(x => x.Player?.Uid == c.Value && x.Room == p.Room && x != p);
                    if (targetPeer == null) { error = "PlayerMissing"; break; }
                    Leave(targetPeer);
                }
                else if (c.Command == Command.Joinable) rooms[room.Id] = room with { Joinable = c.Value != 0 };
                else
                {
                    if (c.Value < 1 || c.Value > 256 || (options.LanKey == null && c.Value > maxPlayers)) { error = "InvalidLimit"; break; }
                    rooms[room.Id] = room with { MaxPlayers = c.Value };
                    if (options.LanKey != null) maxPlayers = c.Value;
                }
                break;
            default: throw new InvalidDataException();
        }
        Publish();
        p.Wire.Send(new(Kind.Ack, Protocol.Pack(c with { Error = error })));
        if (options.LanKey != null && c.Command == Command.Join && error.Length != 0) p.Wire.Finish();
    }

    private void Join(Peer p, Room room, long request)
    { p.Room = room.Id; p.MembershipRequest = request; p.Player = p.Player! with { Membership = checked(++nextMembership), Motion = new(), HasMotion = false }; }

    private void Leave(Peer p)
    {
        if (p.Room == 0) return;
        var room = rooms[p.Room];
        if (room.Host == p.Player!.Uid)
        {
            rooms.Remove(room.Id);
            foreach (var other in peers.Where(x => x.Room == room.Id).ToArray()) ClearRoom(other);
        }
        else ClearRoom(p);
        if (options.LanKey != null)
        {
            if (p.Player!.Uid == lanHost) _ = StopAsync();
            else p.Wire.Close("LeftDefaultRoom");
        }
    }

    private static void ClearRoom(Peer p)
    { p.Room = 0; p.MembershipRequest = 0; p.Player = p.Player! with { Membership = 0, Motion = new(), HasMotion = false }; }

    private void Remove(Peer p)
    {
        if (!peers.Contains(p)) return;
        Leave(p); peers.Remove(p);
        if (p.Player != null) Publish();
        if (options.LanKey != null && p.Player?.Uid == lanHost) _ = StopAsync();
    }

    private void RouteMessage(Peer p, Frame f)
    {
        if (!rules.TryGetValue(f.Type, out var rule) || !rule.Routes.Contains(f.Route) || f.Body.Length > rule.MaxBytes) throw new InvalidDataException("Unregistered route");
        Room? room = rooms.GetValueOrDefault(p.Room);
        if (rule.RoomScoped && (room == null || f.Room != room.Id || f.Membership != p.Player!.Membership)) return;
        if (rule.HostOnly && (room == null || room.Host != p.Player!.Uid)) return;
        if (rule.HostBroadcastOnly && f.Route != Route.Host && room?.Host != p.Player!.Uid) return;
        var source = f with { Sender = p.Player!.Uid, Room = rule.RoomScoped ? p.Room : 0, Membership = rule.RoomScoped ? p.Player.Membership : 0 };
        if (f.Route == Route.Server)
        {
            try { MessageReceived?.Invoke(new(f.Type, new(source.Sender, source.Room, source.Membership, 0, f.Request), f.Body)); }
            catch (Exception e) { try { CallbackError?.Invoke(e); } catch { } }
            return;
        }
        foreach (var target in peers.Where(x => x.Player != null))
        {
            bool send = f.Route switch
            {
                Route.World => target != p,
                Route.Room => target != p && target.Room == p.Room,
                Route.Host => target.Player!.Uid == room?.Host,
                Route.Player => target.Player!.Uid == f.Target,
                _ => false
            };
            if (!send || (rule.RoomScoped && target.Room != p.Room)) continue;
            if (f.RecipientMembership != 0 && f.RecipientMembership != target.Player!.Membership) continue;
            target.Wire.Send(source with { RecipientMembership = rule.RoomScoped ? target.Player!.Membership : 0 });
        }
    }

    private Snapshot Capture(Peer? viewer) => new()
    {
        MaxPlayers = maxPlayers,
        World = peers.Where(p => p.Player != null).Select(p => VisiblePlayer(p, viewer) with { Resources = null }).ToArray(),
        Rooms = rooms.Values.Select(r => new RoomSummary { Id = r.Id, Host = r.Host, MaxPlayers = r.MaxPlayers, Joinable = r.Joinable, Count = peers.Count(p => p.Room == r.Id) }).ToArray(),
        Room = viewer != null && rooms.TryGetValue(viewer.Room, out var room)
            ? room with { Members = peers.Where(p => p.Room == room.Id).Select(p => VisiblePlayer(p, viewer)).ToArray() } : null,
        MembershipRequest = viewer?.MembershipRequest ?? 0
    };

    private static bool CanSeeMotion(Peer source, Peer viewer) => source == viewer
        || (source.Player!.Scene == Scene.DayScene && viewer.Player!.Scene == Scene.DayScene)
        || (source.Player!.Scene == Scene.WorkScene && viewer.Player!.Scene == Scene.WorkScene && source.Room != 0 && source.Room == viewer.Room);

    private static Player VisiblePlayer(Peer source, Peer? viewer) => viewer == null || CanSeeMotion(source, viewer)
        ? source.Player! : source.Player! with { Motion = new(), HasMotion = false };

    private void Publish(Peer? except = null)
    {
        foreach (var p in peers.Where(p => p.Player != null && p != except)) p.Wire.Send(new(Kind.Snapshot, Protocol.Pack(Capture(p))));
    }
    private void Broadcast(Frame frame, Peer except)
    {
        var bytes = Protocol.Encode(frame);
        foreach (var p in peers.Where(p => p.Player != null && p != except)) p.Wire.SendBytes(bytes);
    }

    public async Task<Snapshot> GetSnapshotAsync()
    {
        if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref started) == 0) throw new InvalidOperationException("Not running");
        var result = new TaskCompletionSource<Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        await events.Writer.WriteAsync(() => result.TrySetResult(Protocol.Read<Snapshot>(Protocol.Pack(Capture(null))))).ConfigureAwait(false);
        return await result.Task.ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref stopping, 1) != 0) { await actor.ConfigureAwait(false); return; }
        stop.Cancel(); listener.Stop();
        await Task.WhenAll(accept, clock).ConfigureAwait(false);
        if (Volatile.Read(ref started) == 0) { events.Writer.TryComplete(); return; }
        var drained = new TaskCompletionSource<Task[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Post(() =>
        {
            var connections = peers.Select(p => p.Wire.Completion).ToArray();
            foreach (var p in peers.ToArray()) p.Wire.Close("ServerStopped");
            peers.Clear(); rooms.Clear();
            drained.TrySetResult(connections);
        }).ConfigureAwait(false);
        var completions = await drained.Task.ConfigureAwait(false);
        events.Writer.TryComplete();
        await actor.ConfigureAwait(false);
        await Task.WhenAll(completions).ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

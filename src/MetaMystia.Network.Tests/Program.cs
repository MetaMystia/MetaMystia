using MemoryPack;

using MetaMystia.Network.Core;
using MetaMystia.Protocol;

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS {description}");
}

var endpoint = new Endpoint();
endpoint.Accept(99, 0);
Check(endpoint.OnlineCount == 0, "TCP connection is not a player");
endpoint.Tick(Endpoint.HandshakeTimeoutMs);
Check(endpoint.TryDequeue(out var rejected) && rejected.Close && rejected.Message is Rejected, "Silent handshake expires");
endpoint.Accept(100, 0);
endpoint.Receive(100, new Payload { Route = Route.PublicEvent, Sequence = 1 }, 1);
Check(endpoint.OnlineCount == 0 && endpoint.TryDequeue(out rejected) && rejected.Close, "No relay before handshake");
endpoint.Accept(101, 0);
endpoint.Receive(101, new Hello { Protocol = -1, Name = "old", Application = "test" }, 1);
Check(endpoint.OnlineCount == 0 && endpoint.TryDequeue(out rejected) && rejected.Message is Rejected, "Protocol mismatch rejected");

var cancelled = new ClientSession();
long oldAttempt = cancelled.BeginConnect("old", "test", 0);
cancelled.Disconnect(oldAttempt);
long newAttempt = cancelled.BeginConnect("new", "test", 1);
Check(!cancelled.TransportConnected(oldAttempt, 2) && cancelled.Stage == ConnectionStage.Connecting, "Late TCP success cannot revive cancelled attempt");
cancelled.Tick(20_000);
Check(cancelled.Stage == ConnectionStage.Offline, "Client connect timeout clears state");

var h = new Harness();
var host = h.Add(1, "host");
var guest = h.Add(2, "guest");
var outsider = h.Add(3, "outside");
Check(host.Players.Count == 3 && guest.IsOnline && !guest.IsInRoom, "Handshake establishes public membership only");
h.Request(host, RoomOperation.Create, name: "room-a");
var roomId = host.Room!.Id;
h.Request(guest, RoomOperation.Join, roomId);
Check(guest.IsRoomClient && host.Room.Members.Count == 2 && guest.Players.Count == 3, "Room adds to public membership");
h.Request(guest, RoomOperation.SetCapacity, capacity: 2);
Check(host.Room.Capacity == 4, "Only host can change room capacity");
h.Request(host, RoomOperation.SetCapacity, capacity: 2);
h.Request(outsider, RoomOperation.Join, roomId);
Check(!outsider.IsInRoom && host.Room.Capacity == 2, "Confirmed capacity is enforced on join");
h.Request(host, RoomOperation.SetCapacity, capacity: 4);
h.Request(outsider, RoomOperation.Join, roomId);
h.Request(host, RoomOperation.SetCapacity, capacity: 2);
Check(host.Room.Capacity == 4 && host.Room.Members.Count == 3, "Capacity cannot evict existing members implicitly");
h.Request(host, RoomOperation.Kick, targetUid: outsider.SelfUid);
Check(outsider.IsOnline && !outsider.IsInRoom, "Confirmed kick preserves public connection");
var binding = guest.Binding;
h.DrainEvents();
guest.Send(Route.PublicEvent, 1, new byte[] { 1 });
h.Pump();
Check(h.Payloads(outsider).Count == 1 && h.Payloads(host).Count == 1, "Room member still broadcasts publicly");
h.DrainEvents();
guest.Send(Route.MemberEvent, 2, new byte[] { 2 });
h.Pump();
Check(h.Payloads(outsider).Count == 0 && h.Payloads(host).Count == 1, "Gameplay is isolated to the room");
h.DrainEvents();
h.Endpoint.Receive(2, new Payload { Route = Route.HostEvent, Sequence = 100, SenderUid = host.SelfUid,
    RoomId = roomId, SenderMembershipId = binding.MembershipId, Kind = 3 }, 1);
h.Pump();
Check(h.Payloads(host).Count == 0, "Member cannot forge host route or identity");
h.Request(outsider, RoomOperation.Kick, targetUid: guest.SelfUid);
Check(guest.IsInRoom, "Only host can kick");
h.Request(host, RoomOperation.SetAdmission, admission: false);
h.Request(outsider, RoomOperation.Join, roomId);
Check(!outsider.IsInRoom && host.Room.Members.Count == 2, "Endpoint enforces admission closure");
h.Request(guest, RoomOperation.Leave);
Check(guest.IsOnline && !guest.IsInRoom && guest.Players.Count == 3, "Leaving keeps public connection");
h.Request(host, RoomOperation.SetAdmission, admission: true);
h.Request(guest, RoomOperation.Join, roomId);
Check(guest.Binding != binding, "Rejoin gets a new membership instance");
h.DrainEvents();
guest.Receive(guest.Attempt, new Payload { Route = Route.MemberEvent, SenderUid = host.SelfUid, RoomId = roomId,
    SenderMembershipId = host.Binding.MembershipId, RecipientMembershipId = binding.MembershipId, Sequence = 10, Kind = 4 }, 2);
Check(h.Payloads(guest).Count == 0, "Late message from old binding ignored");

h.Request(guest, RoomOperation.Leave);
h.DrainEvents();
host.Send(Route.MemberState, 10, new byte[] { 1, 2 });
host.Send(Route.MemberState, 10, Array.Empty<byte>());
h.Pump();
h.Request(guest, RoomOperation.Join, roomId);
var restored = h.Payloads(guest).Where(p => p.Kind == 10).ToArray();
Check(restored.Length == 1 && restored[0].Data.Length == 0, "Latest complete state including empty replacement is replayed on join");

guest.Request(RoomOperation.Rename, 100, name: "new-name");
while (guest.TryDequeue(out _)) { }
guest.Tick(100 + ClientSession.RequestTimeoutMs);
Check(guest.Pending?.Reconciling == true && guest.CanPlay, "Non-membership reconciliation does not cancel gameplay in a confirmed room");
h.Pump();

guest.Request(RoomOperation.Leave, 100);
Check(guest.IsInRoom && !guest.CanPlay, "Pending leave does not erase confirmed room");
while (guest.TryDequeue(out _)) { }
guest.Tick(100 + ClientSession.RequestTimeoutMs);
Check(guest.IsInRoom && guest.Pending?.Reconciling == true, "Uncertain result queries authoritative state");
h.Pump();
Check(guest.IsInRoom && guest.Pending == null, "Reconciliation restores confirmed membership without guessing success");
bool reconciled = false;
while (guest.TryDequeueEvent(out var change))
    if (change.Kind == SessionEventKind.RequestCompleted) reconciled |= change.Reconciled;
Check(reconciled, "Query response is distinguished from operation approval");

h.Endpoint.Disconnect(1);
h.Pump();
Check(!guest.IsInRoom && guest.IsOnline && outsider.IsOnline && h.Endpoint.RoomCount == 0, "Host disconnect dissolves room without disconnecting public peers");

var timeout = new Harness();
var silentHost = timeout.Add(10, "silent");
var activeGuest = timeout.Add(11, "active");
timeout.Request(silentHost, RoomOperation.Create, name: "heartbeat");
timeout.Request(activeGuest, RoomOperation.Join, silentHost.Room!.Id);
timeout.Endpoint.Receive(11, new Ping { SentAt = 30_000 }, 30_000);
timeout.Endpoint.Tick(30_001);
timeout.Pump();
Check(activeGuest.IsOnline && !activeGuest.IsInRoom && timeout.Endpoint.OnlineCount == 1, "Server heartbeat expiry removes silent host and closes room");
activeGuest.Tick(60_000);
Check(!activeGuest.IsOnline && activeGuest.Players.Count == 0, "Client heartbeat expiry clears confirmed state");

var resourceRows = Enumerable.Range(0, 9).Select(_ => new[] { 5999, 6000, 8999, 9000 }.AsEnumerable()).ToArray();
var localResources = MetaMystia.ResourceDataBase.FromLocal(resourceRows);
var manifest = MemoryPackSerializer.Deserialize<MetaMystia.ResourceManifest>(MemoryPackSerializer.Serialize(localResources.ToManifest()))!;
Check(localResources.FoodAvailable(5999) && manifest.Categories[0].SequenceEqual(new[] { 6000, 8999, 9000 }), "Resource sync retains local game IDs and transmits only IDs at least 6000");
var remoteResources = MetaMystia.ResourceDataBase.FromManifest(manifest);
manifest.Categories[0][0] = 6001;
Check(remoteResources.FoodAvailable(6000) && !remoteResources.FoodAvailable(6001), "Stored resource snapshot does not alias received arrays");
var emptyManifest = new MetaMystia.ResourceManifest { Categories = Enumerable.Range(0, 9).Select(_ => Array.Empty<int>()).ToArray() };
Check(MetaMystia.ResourceDataBase.ValidManifest(emptyManifest) && !MetaMystia.ResourceDataBase.ValidManifest(new()), "Valid empty resources differ from a missing block");
remoteResources = MetaMystia.ResourceDataBase.FromManifest(emptyManifest);
Check(remoteResources.IsLoaded && !remoteResources.FoodAvailable(9000), "Full resource replacement removes IDs absent from new snapshot");
emptyManifest.Categories[0] = new[] { 5999 };
Check(!MetaMystia.ResourceDataBase.ValidManifest(emptyManifest), "Remote resource manifest rejects IDs below 6000");

Console.WriteLine("All managed state-machine checks passed.");
if (args.Contains("--tcp")) await TcpChecks.Run();

if (args.Length == 2 && args[0] == "--fixtures")
{
    Directory.CreateDirectory(args[1]);
    Message[] fixtures =
    {
        new Hello { Name = "玩家", Application = "test" },
        new RoomCommand { Operation = RoomOperation.Join, RequestId = 123, RoomId = Guid.Parse("73a326d2-ef54-4924-9940-b2f53fe36d99") },
        new Payload { Route = Route.MemberState, Kind = 123, PhaseId = 4, SenderUid = 8, Data = new byte[] { 0, 1, 2 } },
        new Welcome { State = new SessionState { Revision = 2, SelfUid = 1, Players = new[] { new PlayerProfile(1, "玩家") } } }
    };
    for (int i = 0; i < fixtures.Length; i++) File.WriteAllBytes(Path.Combine(args[1], $"{i}.bin"), MemoryPackSerializer.Serialize(fixtures[i]));
}

sealed class Harness
{
    public Endpoint Endpoint { get; } = new();
    private readonly Dictionary<long, ClientSession> _clients = new();

    public ClientSession Add(long id, string name)
    {
        var client = new ClientSession();
        _clients.Add(id, client);
        Endpoint.Accept(id, 0);
        client.TransportConnected(client.BeginConnect(name, "test", 0), 0);
        Pump();
        return client;
    }

    public void Request(ClientSession client, RoomOperation operation, Guid room = default, string name = "", int targetUid = -1, bool admission = false, int capacity = 4)
    {
        if (!client.Request(operation, 0, room, name, capacity, targetUid: targetUid, admissionOpen: admission)) throw new InvalidOperationException("Request not submitted");
        Pump();
    }

    public void Pump()
    {
        for (int iteration = 0; iteration < 100; iteration++)
        {
            bool work = false;
            foreach (var pair in _clients)
                while (pair.Value.TryDequeue(out var message))
                { Endpoint.Receive(pair.Key, Clone(message), 1); work = true; }
            while (Endpoint.TryDequeue(out var delivery))
            {
                if (_clients.TryGetValue(delivery.ConnectionId, out var client))
                {
                    client.Receive(client.Attempt, Clone(delivery.Message), 1);
                    if (delivery.Close) client.Disconnect(client.Attempt);
                }
                work = true;
            }
            if (!work) return;
        }
        throw new InvalidOperationException("State-machine did not settle");
    }

    public void DrainEvents() { foreach (var client in _clients.Values) Payloads(client); }
    public List<Payload> Payloads(ClientSession client)
    {
        var result = new List<Payload>();
        while (client.TryDequeueEvent(out var change)) if (change.Payload != null) result.Add(change.Payload);
        return result;
    }
    private static Message Clone(Message value) => MemoryPackSerializer.Deserialize<Message>(MemoryPackSerializer.Serialize(value))!;
}

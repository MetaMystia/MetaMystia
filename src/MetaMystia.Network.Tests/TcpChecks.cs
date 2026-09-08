using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

using MetaMystia.Network.Core;
using MetaMystia.Protocol;

static class TcpChecks
{
    public static async Task Run()
    {
        using var host = new EndpointHost(0, bindAddress: IPAddress.Loopback);
        var local = new ClientSession();
        var remote = new ClientSession();
        var localPayloads = new List<Payload>();
        long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        host.AttachLocal(Now());
        local.TransportConnected(local.BeginConnect("embedded", "tcp-test", Now()), Now());
        long attempt = remote.BeginConnect("remote", "tcp-test", Now());
        using var socket = await TcpConnection.ConnectAsync("127.0.0.1", host.Port, CancellationToken.None);
        remote.TransportConnected(attempt, Now());

        void Pump()
        {
            while (local.TryDequeue(out var message)) host.ReceiveLocal(message, Now());
            while (remote.TryDequeue(out var message)) socket.Send(message);
            host.Pump(Now());
            while (host.TryDequeueLocal(out var delivery)) local.Receive(local.Attempt, delivery.Message, Now());
            while (socket.TryDequeue(out var received))
            {
                if (received.Message != null) remote.Receive(attempt, received.Message, Now());
                else remote.Disconnect(attempt, received.Error);
            }
            while (local.TryDequeueEvent(out var change)) if (change.Payload != null) localPayloads.Add(change.Payload);
            local.Tick(Now());
            remote.Tick(Now());
        }
        void Until(Func<bool> predicate, string description)
        {
            long deadline = Now() + 5_000;
            while (!predicate() && Now() < deadline) { Pump(); Thread.Sleep(2); }
            if (!predicate()) throw new InvalidOperationException(description);
            Console.WriteLine($"PASS TCP {description}");
        }

        Until(() => local.IsOnline && remote.IsOnline && local.Players.Count == 2, "embedded and TCP clients share handshake rules");
        local.Request(RoomOperation.Create, Now(), name: "tcp-room");
        Until(() => local.IsRoomHost, "room creation is confirmed");
        remote.Request(RoomOperation.Join, Now(), local.Room!.Id);
        Until(() => remote.IsInRoom && local.Room.Members.Count == 2, "remote room admission is confirmed");
        remote.Send(Route.MemberEvent, 1, new byte[] { 3, 4, 5 }, phaseId: 7);
        Until(() => localPayloads.Any(p => p.Kind == 1 && p.PhaseId == 7 && p.SenderUid == remote.SelfUid && p.Data.SequenceEqual(new byte[] { 3, 4, 5 })),
            "framed payload retains actual sender and phase");
        local.Request(RoomOperation.Leave, Now());
        Until(() => local.Room == null && remote.Room == null && remote.IsOnline, "host leave dissolves room while keeping public TCP session");
        remote.Send(Route.PublicEvent, 2, new byte[] { 9 });
        Until(() => localPayloads.Any(p => p.Kind == 2), "public messages work after room dissolution");

        foreach (int length in new[] { 0, -1, ProtocolVersion.MaxFrameBytes + 1 })
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var sender = new TcpClient();
            await sender.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            using var receiver = new TcpConnection(await listener.AcceptTcpClientAsync());
            listener.Stop();
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, length);
            await sender.GetStream().WriteAsync(header);
            TransportEvent? disconnected = null;
            Until(() =>
            {
                if (receiver.TryDequeue(out var received)) disconnected = received;
                return disconnected?.Message == null && disconnected?.Error == "Invalid frame length" && receiver.IsClosed;
            },
                $"invalid frame length {length} closes transport and reports disconnection");
        }
    }
}

using System.Buffers.Binary;
using System.Text;

using MemoryPack;

namespace MetaMystia.Network;

internal enum Kind : byte { Hello = 1, Rejected = 2, Welcome = 3, Snapshot = 4, Command = 5, Ack = 6, Motion = 7, Profile = 8, Data = 9, Ping = 10, Pong = 11, RoomMotion = 12 }
internal enum Command : byte { Create = 1, Join = 2, Cancel = 3, Leave = 4, Joinable = 5, Limit = 6, Kick = 7 }

[MemoryPackable]
internal partial record Control
{
    public Command Command { get; init; }
    public long Request { get; init; }
    public long Room { get; init; }
    public long Membership { get; init; }
    public long CancelRequest { get; init; }
    public int Value { get; init; }
    public string Error { get; init; } = "";
}

internal sealed record Frame(Kind Kind, byte[] Body, int Sender = 0, ushort Type = 0,
    Route Route = Route.Server, int Target = 0, long Room = 0, long Membership = 0,
    long RecipientMembership = 0, long Request = 0);

internal static class Protocol
{
    internal const int MaxFrame = 256 * 1024;
    internal const int QueueCapacity = 256;
    internal static byte[] Pack<T>(T value) => MemoryPackSerializer.Serialize(value);
    internal static T Read<T>(byte[] body) => MemoryPackSerializer.Deserialize<T>(body) ?? throw new InvalidDataException("Empty payload");

    internal static byte[] Encode(Frame f)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write((byte)f.Kind);
        if (f.Kind is Kind.Motion or Kind.RoomMotion or Kind.Profile or Kind.Welcome) w.Write(f.Sender);
        if (f.Kind == Kind.RoomMotion) { w.Write(f.Room); w.Write(f.Membership); w.Write(f.RecipientMembership); }
        if (f.Kind == Kind.Data)
        {
            w.Write(f.Type); w.Write((byte)f.Route); w.Write(f.Sender); w.Write(f.Request);
            if (f.Route == Route.Player) w.Write(f.Target);
            if (f.Route != Route.World)
            { w.Write(f.Room); w.Write(f.Membership); w.Write(f.RecipientMembership); }
        }
        w.Write(f.Body);
        if (stream.Length > MaxFrame) throw new InvalidDataException("Frame too large");
        var result = new byte[4 + stream.Length];
        BinaryPrimitives.WriteInt32LittleEndian(result, (int)stream.Length);
        stream.GetBuffer().AsSpan(0, (int)stream.Length).CopyTo(result.AsSpan(4));
        return result;
    }

    internal static Frame Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        using var r = new BinaryReader(stream);
        var kind = (Kind)r.ReadByte();
        if (!Enum.IsDefined(typeof(Kind), kind)) throw new InvalidDataException("Unknown kind");
        int sender = 0, target = 0;
        ushort type = 0;
        long room = 0, member = 0, recipient = 0, request = 0;
        var route = Route.Server;
        if (kind is Kind.Motion or Kind.RoomMotion or Kind.Profile or Kind.Welcome) sender = r.ReadInt32();
        if (kind == Kind.RoomMotion) { room = r.ReadInt64(); member = r.ReadInt64(); recipient = r.ReadInt64(); }
        if (kind == Kind.Data)
        {
            type = r.ReadUInt16(); route = (Route)r.ReadByte(); sender = r.ReadInt32(); request = r.ReadInt64();
            if (route == Route.Player) target = r.ReadInt32();
            if (route != Route.World) { room = r.ReadInt64(); member = r.ReadInt64(); recipient = r.ReadInt64(); }
        }
        return new(kind, r.ReadBytes((int)(stream.Length - stream.Position)), sender, type, route, target, room, member, recipient, request);
    }

    // Hello 外壳不依赖 MemoryPack；版本失败始终使用 UTF-8 错误码。
    internal static byte[] Hello(Versions v, Player player, string key)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream, Encoding.UTF8);
        w.Write(0x3152494d); w.Write(v.Protocol); w.Write(v.Game); w.Write(v.Mod); w.Write(key);
        w.Write(Pack(player));
        return stream.ToArray();
    }

    internal static (Player Player, string Key) ReadHello(byte[] body, Versions expected)
    {
        using var stream = new MemoryStream(body, false);
        using var r = new BinaryReader(stream, Encoding.UTF8);
        if (r.ReadInt32() != 0x3152494d || r.ReadInt32() != expected.Protocol) throw new NetworkException("ProtocolMismatch");
        if (r.ReadString() != expected.Game) throw new NetworkException("GameMismatch");
        if (r.ReadString() != expected.Mod) throw new NetworkException("ModMismatch");
        var key = r.ReadString();
        if (key.Length > 128) throw new InvalidDataException();
        return (Read<Player>(r.ReadBytes((int)(stream.Length - stream.Position))), key);
    }

    internal static void Validate(Player p, bool resources)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 64 || p.Name.Any(c => c is '<' or '>' || char.IsWhiteSpace(c) || char.IsControl(c)) || p.Skin == null ||
            p.Skin.NetSkinName == null || p.Skin.NetSkinName.Length > 128 || p.Skin.ResourcePackId == null ||
            p.Skin.ResourcePackId.Length > 128 || !Enum.IsDefined(typeof(Common.UI.Scene), p.Scene)) throw new InvalidDataException("Invalid profile");
        Validate(p.Motion);
        if (!resources) return;
        var res = p.Resources;
        if (res == null || !res.Ready || res.PackIds == null || res.PackIds.Length > 128 ||
            res.PackIds.Any(s => string.IsNullOrEmpty(s) || s.Length > 128) || res.ExtraIds == null ||
            res.ExtraIds.Length != 9 || res.ExtraIds.Any(a => a == null || a.Length > 2048) ||
            Pack(res).Length > 8192) throw new InvalidDataException("ResourcesNotReadyOrInvalid");
    }

    internal static void Validate(Motion m)
    {
        if (m == null || !float.IsFinite(m.X) || !float.IsFinite(m.Y) || !float.IsFinite(m.DirectionX) ||
            !float.IsFinite(m.DirectionY) || !float.IsFinite(m.Speed) || !Enum.IsDefined(typeof(MapLabel), m.Map)) throw new InvalidDataException("Invalid motion");
    }
}

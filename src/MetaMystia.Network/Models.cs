using MemoryPack;

using Common.UI;
using GameData.Core.Collections.CharacterUtility;

namespace MetaMystia.Network;

public sealed partial record Versions(int Protocol, string Game, string Mod);


[MemoryPackable]
public partial record Skin
{
    public int CharacterId { get; init; } = -1;
    public CharacterSkinSets.SelectedType SelectedType { get; init; }
    public int SkinIndex { get; init; }
    public string NetSkinName { get; init; } = "";
    public bool? RotateOverride { get; init; }
    // 自定义角色在世界范围显示时所需的稳定资源身份。
    public string ResourcePackId { get; init; } = "";
}

[MemoryPackable]
public partial record Resources
{
    public bool Ready { get; init; }
    public DlcPack DlcFlags { get; init; }
    public string[] PackIds { get; init; } = [];
    public int[][] ExtraIds { get; init; } = Enumerable.Range(0, 9).Select(_ => Array.Empty<int>()).ToArray();
}


[MemoryPackable]
public partial record Motion
{
    public float X { get; init; }
    public float Y { get; init; }
    public float DirectionX { get; init; }
    public float DirectionY { get; init; }
    public float Speed { get; init; }
    public bool Sprinting { get; init; }
    public MapLabel Map { get; init; }
}

[MemoryPackable]
public partial record Player
{
    public int Uid { get; init; }
    public string Name { get; init; } = "";
    public Skin Skin { get; init; } = new();
    public Resources? Resources { get; init; }
    public Motion Motion { get; init; } = new();
    public bool HasMotion { get; init; }
    public Scene Scene { get; init; }
    public long Membership { get; init; }
}

[MemoryPackable]
public partial record Room
{
    public long Id { get; init; }
    public int Host { get; init; }
    public int MaxPlayers { get; init; }
    public bool Joinable { get; init; }
    public Player[] Members { get; init; } = [];
}

[MemoryPackable]
public partial record RoomSummary
{
    public long Id { get; init; }
    public int Host { get; init; }
    public int Count { get; init; }
    public int MaxPlayers { get; init; }
    public bool Joinable { get; init; }
}

[MemoryPackable]
public partial record Snapshot
{
    public int MaxPlayers { get; init; }
    public Player[] World { get; init; } = [];
    public RoomSummary[] Rooms { get; init; } = [];
    public Room? Room { get; init; }
    public long MembershipRequest { get; init; }

    public Snapshot Copy() => this with
    {
        World = World.Select(CopyPlayer).ToArray(),
        Rooms = Rooms.ToArray(),
        Room = Room is { } room ? room with { Members = room.Members.Select(CopyPlayer).ToArray() } : null
    };

    private static Player CopyPlayer(Player player) => player with
    {
        Resources = player.Resources is { } resources ? resources with
        {
            PackIds = resources.PackIds.ToArray(),
            ExtraIds = resources.ExtraIds.Select(ids => ids.ToArray()).ToArray()
        } : null
    };
}

public enum Route : byte { Server, World, Room, Host, Player }
public sealed record MessageRule(ushort Id, Route[] Routes, bool HostOnly = false, bool RoomScoped = true, int MaxBytes = 65536, bool HostBroadcastOnly = false);
public static class Messages
{
    public const ushort Chat = 1, GameplayRequest = 2, GameplayResult = 3;
    public static MessageRule[] DefaultRules() =>
    [
        new(Chat, [Route.World], RoomScoped: false, MaxBytes: 4096),
        new(GameplayRequest, [Route.Host]),
        new(GameplayResult, [Route.Room, Route.Player], HostOnly: true)
    ];
}
public sealed record MessageContext(int Sender, long Room, long SenderMembership, long RecipientMembership, long Request);
public sealed record ReceivedMessage(ushort Type, MessageContext Context, byte[] Body);
public sealed record ServerOptions
{
    public System.Net.IPAddress Address { get; init; } = System.Net.IPAddress.Loopback;
    public int Port { get; init; }
    public int MaxPlayers { get; init; } = 16;
    public Versions Versions { get; init; } = Versions.Current;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public MessageRule[] Messages { get; init; } = MetaMystia.Network.Messages.DefaultRules();
    internal string? LanKey { get; init; }
}

public sealed class NetworkException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

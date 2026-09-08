using System;

using MemoryPack;

namespace MetaMystia.Protocol;

public static class ProtocolVersion
{
    public const int Current = 1;
    public const int MaxFrameBytes = 4 * 1024 * 1024;
}

// 这里的枚举只描述联机规则。游戏枚举留在 Mod 的不透明载荷中。
public enum Route : byte { PublicState, PublicEvent, MemberState, MemberEvent, HostRequest, HostEvent, HostState }
public enum RoomOperation : byte { Create, Join, Leave, Kick, SetAdmission, Query, Rename, SetCapacity }

[MemoryPackable]
[MemoryPackUnion(0, typeof(Hello))]
[MemoryPackUnion(1, typeof(Welcome))]
[MemoryPackUnion(2, typeof(RoomCommand))]
[MemoryPackUnion(3, typeof(SessionState))]
[MemoryPackUnion(4, typeof(Payload))]
[MemoryPackUnion(5, typeof(Ping))]
[MemoryPackUnion(6, typeof(Pong))]
[MemoryPackUnion(7, typeof(Rejected))]
public abstract partial class Message;

[MemoryPackable]
public sealed partial class Hello : Message
{
    public int Protocol { get; set; } = ProtocolVersion.Current;
    public string Application { get; set; } = "";
    public string Name { get; set; } = "";
}

[MemoryPackable]
public sealed partial class Welcome : Message
{
    public SessionState State { get; set; } = new();
    public Guid DefaultRoom { get; set; }
}

[MemoryPackable]
public sealed partial class RoomCommand : Message
{
    public long RequestId { get; set; }
    public RoomOperation Operation { get; set; }
    public Guid RoomId { get; set; }
    public long MembershipId { get; set; }
    public int TargetUid { get; set; } = -1;
    public string Name { get; set; } = "";
    public int Capacity { get; set; } = 4;
    public bool AdmissionOpen { get; set; }
}

[MemoryPackable]
public sealed partial record PlayerProfile(int Uid, string Name);

[MemoryPackable]
public sealed partial record RoomMember(int Uid, long MembershipId);

[MemoryPackable]
public sealed partial record RoomSummary(Guid Id, string Name, int HostUid, int Count, int Capacity, bool AdmissionOpen);

[MemoryPackable]
public sealed partial record RoomSnapshot(Guid Id, string Name, int HostUid, int Capacity, bool AdmissionOpen, long Revision, RoomMember[] Members);

[MemoryPackable]
public sealed partial class SessionState : Message
{
    public long Revision { get; set; }
    public long RequestId { get; set; }
    public string Error { get; set; } = "";
    public int SelfUid { get; set; } = -1;
    public PlayerProfile[] Players { get; set; } = Array.Empty<PlayerProfile>();
    public RoomSummary[] Rooms { get; set; } = Array.Empty<RoomSummary>();
    public RoomSnapshot? Room { get; set; }
}

[MemoryPackable]
public sealed partial class Payload : Message
{
    public Route Route { get; set; }
    public ushort Kind { get; set; }
    public long Sequence { get; set; }
    public int SenderUid { get; set; } = -1;
    public int TargetUid { get; set; } = -1;
    public Guid RoomId { get; set; }
    public long SenderMembershipId { get; set; }
    public long RecipientMembershipId { get; set; }
    public long PhaseId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public static bool IsPublic(Route route) => route is Route.PublicState or Route.PublicEvent;
    public static bool IsState(Route route) => route is Route.PublicState or Route.MemberState or Route.HostState;
}

[MemoryPackable]
public sealed partial class Ping : Message
{
    public long SentAt { get; set; }
}

[MemoryPackable]
public sealed partial class Pong : Message
{
    public long SentAt { get; set; }
    public long ServerTime { get; set; }
}

[MemoryPackable]
public sealed partial class Rejected : Message
{
    public string Reason { get; set; } = "";
}

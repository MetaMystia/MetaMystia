namespace MetaMystia.Discovery;

public sealed record ServerListing(string InstanceId, string GameVersion, string ModVersion,
    string State, string? Host, int? Port, string? TargetModVersion);
public sealed record ServerCatalog(ServerListing[] Servers);
public sealed record InstanceRegistration(string GameVersion, string ModVersion, string Host, int Port, string AdminUrl);
public sealed record ActivationRequest(string[] Replaces);
public sealed record DrainRequest(string NoticeId, string TargetModVersion);
public sealed record InstanceStatus(string InstanceId, string GameVersion, string ModVersion, int Port,
    string State, int Players, string? NoticeId, string? TargetModVersion);
public sealed record InstanceRecord(string Id, InstanceRegistration Registration, string State = "staged",
    string? NoticeId = null, string? TargetModVersion = null, InstanceStatus? Observed = null,
    DateTimeOffset? LastSeen = null, bool DrainConfirmed = false);

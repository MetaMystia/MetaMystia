namespace MetaMystia.Network;

public sealed record DrainRequest(string NoticeId, string TargetModVersion);
public sealed record InstanceStatus(string InstanceId, string GameVersion, string ModVersion, int Port,
    string State, int Players, string? NoticeId, string? TargetModVersion);

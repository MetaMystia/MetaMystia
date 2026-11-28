using System.Collections.Generic;

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>料理对决队伍表与邀请的完整快照，由房主广播。</summary>
[MemoryPackable]
public partial class VersusStateMessage : MultiplayerMessage
{
    public bool Open { get; set; }
    public int Invite { get; set; }
    public VersusTempo Tempo { get; set; }
    public Dictionary<int, VersusTeam> Teams { get; set; } = new();
    public List<int> Confirmed { get; set; } = new();
    public List<int> MissingDlc3 { get; set; } = new();

    [RequireHostSender]
    [ClientOnlyReceive]
    public override void OnReceivedDerived() => VersusManager.ApplyState(this);

    public static void Send() => new VersusStateMessage
    {
        Open = VersusRoster.Open,
        Invite = VersusRoster.Invite,
        Tempo = VersusRoster.Tempo,
        Teams = new(VersusRoster.Teams),
        Confirmed = new(VersusRoster.Confirmed),
        MissingDlc3 = new(VersusRoster.MissingDlc3),
    }.Enqueue();
}

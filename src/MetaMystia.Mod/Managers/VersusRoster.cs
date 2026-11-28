using System.Collections.Generic;
using System.Linq;

using MetaMystia.Multiplayer;

namespace MetaMystia;

public enum VersusTeam : byte
{
    None,
    Red,
    Blue,
    Watch,
}

public enum VersusTempo : byte
{
    Slow,
    Normal,
    Fast,
}

/// <summary>料理对决的房间队伍表。房主修改后广播完整快照，客机只应用快照。</summary>
public static class VersusRoster
{
    public static readonly Dictionary<int, VersusTeam> Teams = new();
    // 本次邀请中已选队的成员；队伍跨场次保留，选队按邀请重新确认。
    public static readonly HashSet<int> Confirmed = new();
    public static readonly HashSet<int> MissingDlc3 = new();
    public static bool Open { get; private set; }
    public static int Invite { get; private set; }
    public static VersusTempo Tempo { get; private set; } = VersusTempo.Normal;

    public static VersusTeam TeamOf(int uid) => Teams.GetValueOrDefault(uid);

    public static int Count(VersusTeam team) => Teams.Values.Count(value => value == team);

    public static bool CanStart() => Open && GameSession.Room?.Members is { Length: > 0 } members
        && members.All(member => Confirmed.Contains(member.Uid)) && MissingDlc3.Count == 0
        && members.Any(member => TeamOf(member.Uid) == VersusTeam.Red)
        && members.Any(member => TeamOf(member.Uid) == VersusTeam.Blue);

    public static void Assign(int uid)
    {
        if (!Teams.ContainsKey(uid))
            Teams[uid] = Count(VersusTeam.Red) <= Count(VersusTeam.Blue) ? VersusTeam.Red : VersusTeam.Blue;
    }

    /// <summary>房主改队，不改变成员本次邀请的选队状态。</summary>
    public static void SetTeam(int uid, VersusTeam team) => Teams[uid] = team;

    /// <summary>非旁观成员随机分到红蓝两队，人数差不超过一。</summary>
    public static void Shuffle(IEnumerable<int> members, System.Random random)
    {
        var players = members.Where(uid => TeamOf(uid) != VersusTeam.Watch).OrderBy(_ => random.Next()).ToList();
        for (int i = 0; i < players.Count; i++) Teams[players[i]] = i % 2 == 0 ? VersusTeam.Red : VersusTeam.Blue;
    }

    public static void Remove(int uid)
    {
        Teams.Remove(uid);
        Confirmed.Remove(uid);
        MissingDlc3.Remove(uid);
    }

    public static void OpenInvite(VersusTempo tempo)
    {
        Open = true;
        Invite++;
        Tempo = tempo;
        Confirmed.Clear();
        MissingDlc3.Clear();
    }

    public static void Close()
    {
        Open = false;
        Confirmed.Clear();
        MissingDlc3.Clear();
    }

    /// <summary>成员选队；<see cref="VersusTeam.None"/> 表示本次邀请暂不参加，保留原队伍。</summary>
    public static void Choose(int uid, VersusTeam team, bool hasDlc3)
    {
        if (team == VersusTeam.None)
        {
            Confirmed.Remove(uid);
            MissingDlc3.Remove(uid);
            return;
        }
        Teams[uid] = team;
        Confirmed.Add(uid);
        if (hasDlc3) MissingDlc3.Remove(uid);
        else MissingDlc3.Add(uid);
    }

    public static void Apply(bool open, int invite, VersusTempo tempo, Dictionary<int, VersusTeam> teams,
        IEnumerable<int> confirmed, IEnumerable<int> missingDlc3)
    {
        Open = open;
        Invite = invite;
        Tempo = tempo;
        Teams.Clear();
        foreach (var pair in teams) Teams[pair.Key] = pair.Value;
        Confirmed.Clear();
        Confirmed.UnionWith(confirmed);
        MissingDlc3.Clear();
        MissingDlc3.UnionWith(missingDlc3);
    }

    public static void Reset()
    {
        Teams.Clear();
        Close();
        Invite = 0;
        Tempo = VersusTempo.Normal;
    }
}

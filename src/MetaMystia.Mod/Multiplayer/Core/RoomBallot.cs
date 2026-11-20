using System;
using System.Collections.Generic;
using System.Linq;

using MetaMystia.Network;

namespace MetaMystia.Multiplayer;

/// <summary>房主按当前房间成员汇总各端提交的值；成员只取房间快照，离房成员的值不计入。</summary>
public sealed class RoomBallot<T>
{
    private readonly Dictionary<int, T> votes = new();
    private T submitted;

    /// <summary>提交本端的值：房主当帧计入，客机发给房主；与上次提交相同时忽略。</summary>
    public void Submit(T value, Action<T> send)
    {
        if (EqualityComparer<T>.Default.Equals(submitted, value)) return;
        submitted = value;
        if (GameSession.IsRoomHost) Receive(PlayerManager.Local.Uid, value);
        else send(value);
    }

    public void Receive(int uid, T value)
    {
        if (GameSession.Room?.Members.Any(member => member.Uid == uid) == true) votes[uid] = value;
    }

    /// <summary>每名未豁免的成员都已提交且被接受。</summary>
    public bool All(Func<Player, T, bool> accepts, Func<Player, bool> exempt = null) =>
        GameSession.Room?.Members.All(member => exempt?.Invoke(member) == true
            || votes.TryGetValue(member.Uid, out var value) && accepts(member, value)) == true;
}

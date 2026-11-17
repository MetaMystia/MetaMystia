using MetaMystia;
using MetaMystia.Multiplayer;
using MetaMystia.Network;

static class CoreChecks
{
    static readonly List<string> ended = new();

    sealed class CoopProbe : IScopeEnd
    {
        public void OnScopeEnd(bool resume) => ended.Add($"coop:{resume}:{Scopes.Coop.Get<CoopProbe>() != this}");
    }

    sealed class SceneProbe : IScopeEnd
    {
        public void OnScopeEnd(bool resume) => ended.Add($"scene:{resume}");
    }

    public static void Run(Action<bool, string> check)
    {
        Scopes.EndCoop();
        ended.Clear();
        var coop = Scopes.Coop;
        var probe = coop.Get<CoopProbe>();
        Scopes.Scene.Get<SceneProbe>();
        check(ReferenceEquals(probe, coop.Get<CoopProbe>()), "同一作用域内取到同一份状态");
        Scopes.EndCoop(resume: true);
        check(ended.SequenceEqual(["scene:True", "coop:True:True"]),
            "结束共同推进时先结束场景，收尾时已换上新作用域");
        check(coop.Ended && !Scopes.Coop.Ended, "保存的旧作用域标记为已结束");
        Scopes.EndScene();
        check(ended.Count == 2, "结束场景不影响共同推进");

        GameSession.IsInRoom = true;
        GameSession.IsRoomHost = true;
        GameSession.Room = new() { Host = 1, Members = [new() { Uid = 1, Stage = GameStage.Work }, new() { Uid = 2, Stage = GameStage.Work }] };
        PlayerManager.Local = new() { Uid = 1 };
        var sent = new List<bool>();
        var ballot = new RoomBallot<bool>();
        ballot.Receive(3, true);
        ballot.Submit(true, sent.Add);
        check(sent.Count == 0 && !ballot.All((_, value) => value), "房主当帧计入本端，仍等待其他成员");
        ballot.Receive(2, true);
        check(ballot.All((_, value) => value), "全员提交后通过");
        GameSession.Room = GameSession.Room with { Members = [.. GameSession.Room.Members, new() { Uid = 3, Stage = GameStage.Work }] };
        check(!ballot.All((_, value) => value), "入房前提交的值不计入");
        check(ballot.All((_, value) => value, member => member.Uid == 3), "豁免成员不需要提交");
        GameSession.Room = GameSession.Room with { Members = GameSession.Room.Members.Where(member => member.Uid != 3).ToArray() };
        check(ballot.All((_, value) => value), "离房成员不再阻塞");
        GameSession.IsRoomHost = false;
        var client = new RoomBallot<bool>();
        client.Submit(false, sent.Add);
        client.Submit(true, sent.Add);
        client.Submit(true, sent.Add);
        check(sent.SequenceEqual([true]), "客机只在提交值变化时发送");
        GameSession.IsRoomHost = true;
    }
}

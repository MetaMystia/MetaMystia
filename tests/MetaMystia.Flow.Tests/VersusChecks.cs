using MetaMystia;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

static class VersusChecks
{
    static void Setup()
    {
        PluginHost.Instance.Clear();
        GameSession.IsInRoom = true;
        GameSession.IsRoomHost = true;
        GameSession.Admission = Task.CompletedTask;
        GameSession.AdmissionCompleted = null;
        GameSession.Room = new() { Host = 1, Members = [new() { Uid = 1, Stage = GameStage.Day }, new() { Uid = 2, Stage = GameStage.Day }] };
        PlayerManager.Local = new() { Uid = 1 };
        PlayerManager.Peers = new() { [2] = new() { Uid = 2 } };
        GameFlow.LocalScene = Scene.DayScene;
        GameFlow.Destination = DayDestination.None;
        DayDestinationManager.ResetSession();
        VersusRoster.Reset();
        Scopes.EndCoop();
        DayDestinationConfirmMessage.Sent.Clear();
        BusinessStartMessage.Sent.Clear();
    }

    static void BothIntend(DayDestination local, DayDestination peer)
    {
        DayDestinationManager.Submit(local, () => { });
        DayDestinationManager.ReceiveIntent(2, DayDestinationManager.Round, peer);
        DayDestinationManager.TryConfirm();
        PluginHost.Instance.Tick();
        DayDestinationManager.TryConfirm();
    }

    public static void Run(Action<bool, string> check)
    {
        Setup();
        VersusRoster.OpenInvite(VersusTempo.Normal);
        VersusRoster.Assign(1);
        VersusRoster.Assign(2);
        VersusRoster.Assign(3);
        check(VersusRoster.TeamOf(1) == VersusTeam.Red && VersusRoster.TeamOf(2) == VersusTeam.Blue
            && VersusRoster.TeamOf(3) == VersusTeam.Red, "新成员按人数轮流分到红蓝两队");
        VersusRoster.Assign(1);
        check(VersusRoster.Count(VersusTeam.Red) == 2, "已有队伍的成员不重新分队");

        Setup();
        VersusRoster.OpenInvite(VersusTempo.Normal);
        VersusRoster.Choose(1, VersusTeam.Red, true);
        VersusRoster.Choose(2, VersusTeam.Red, true);
        BothIntend(DayDestination.Versus, DayDestination.Versus);
        check(DayDestinationConfirmMessage.Sent.Count == 0, "只有一队有人时不确认料理对决");
        VersusRoster.Choose(2, VersusTeam.Blue, false);
        DayDestinationManager.TryConfirm();
        PluginHost.Instance.Tick();
        DayDestinationManager.TryConfirm();
        check(DayDestinationConfirmMessage.Sent.Count == 0, "有成员缺少 DLC3 时不确认料理对决");
        VersusRoster.Choose(2, VersusTeam.Blue, true);
        DayDestinationManager.TryConfirm();
        PluginHost.Instance.Tick();
        DayDestinationManager.TryConfirm();
        check(DayDestinationConfirmMessage.Sent.SingleOrDefault().Target == DayDestination.Versus
            && GameFlow.Destination == DayDestination.Versus, "红蓝各有人且全员已选队后确认料理对决");

        Setup();
        VersusRoster.OpenInvite(VersusTempo.Normal);
        VersusRoster.Choose(1, VersusTeam.Red, true);
        VersusRoster.Choose(2, VersusTeam.Blue, true);
        BothIntend(DayDestination.Versus, DayDestination.Business);
        check(DayDestinationConfirmMessage.Sent.Count == 0, "有人选营业、有人选对决时不确认");

        Setup();
        VersusRoster.OpenInvite(VersusTempo.Normal);
        VersusRoster.Choose(1, VersusTeam.Red, true);
        VersusRoster.Choose(2, VersusTeam.Blue, true);
        VersusRoster.Choose(2, VersusTeam.None, true);
        BothIntend(DayDestination.Versus, DayDestination.Versus);
        check(DayDestinationConfirmMessage.Sent.Count == 0 && VersusRoster.TeamOf(2) == VersusTeam.Blue,
            "暂不参加的成员保留原队伍，但阻止本次对决");

        Setup();
        VersusRoster.OpenInvite(VersusTempo.Normal);
        VersusRoster.Choose(1, VersusTeam.Red, true);
        VersusRoster.Choose(2, VersusTeam.Blue, true);
        VersusRoster.OpenInvite(VersusTempo.Fast);
        check(VersusRoster.Confirmed.Count == 0 && VersusRoster.TeamOf(2) == VersusTeam.Blue && VersusRoster.Invite == 2,
            "新邀请清空选队确认，保留队伍");
        VersusRoster.Remove(2);
        check(VersusRoster.TeamOf(2) == VersusTeam.None, "离房成员移出队伍表");

        VersusRoster.Reset();
        VersusRoster.SetTeam(4, VersusTeam.Watch);
        VersusRoster.Shuffle([1, 2, 3, 4, 5], new Random(7));
        check(VersusRoster.TeamOf(4) == VersusTeam.Watch && System.Math.Abs(VersusRoster.Count(VersusTeam.Red) - VersusRoster.Count(VersusTeam.Blue)) == 0
            && VersusRoster.Count(VersusTeam.Red) == 2, "随机分队保留旁观，红蓝人数差不超过一");

        check(GameFlow.CanKeepRoom(Scene.DayScene, Scene.WorkScene, DayDestination.Versus, true, false), "已确认对决入口保留房间");
        check(GameFlow.CanKeepRoom(Scene.WorkScene, Scene.DayScene, DayDestination.Versus, false, true), "对决正常返回保留房间");
        check(!GameFlow.CanKeepRoom(Scene.WorkScene, Scene.DayScene, DayDestination.Versus, false, false), "对决非正常返回退房");
        check(!GameFlow.CanKeepRoom(Scene.DayScene, Scene.IzakayaPrepScene, DayDestination.Versus, false, false), "对决不经过备菜");

        Setup();
        GameFlow.Destination = DayDestination.Versus;
        GameFlow.LocalScene = Scene.WorkScene;
        GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Work }, new() { Uid = 2, Stage = GameStage.Work }] };
        check(BusinessStart.IsWaitingForStart, "对决开场前等待全员就绪");
        int started = 0;
        BusinessStart.Wait(() => started++);
        BusinessStart.TryStart();
        check(started == 0, "对端未就绪时不放行对决");
        BusinessStart.ReceiveReady(2);
        BusinessStart.TryStart();
        check(started == 1 && !BusinessStart.IsWaitingForStart && BusinessStartMessage.Sent.SequenceEqual([true]),
            "全员就绪后放行对决开场");
    }
}

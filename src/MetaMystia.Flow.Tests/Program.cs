using Common.UI;

using MetaMystia;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}
void Setup(bool host = true)
{
    PluginHost.Instance.Clear();
    GameSession.IsInRoom = true;
    GameSession.IsConnecting = false;
    GameSession.State = new();
    GameSession.Leaves = GameSession.Stops = 0;
    GameSession.Resumed = false;
    GameSession.IsRoomHost = host;
    GameSession.Client = new();
    GameSession.Admission = Task.CompletedTask;
    GameSession.AdmissionCompleted = null;
    GameSession.Room = new() { Host = 1, Members = [new() { Uid = 1, Stage = GameStage.Day }, new() { Uid = 2, Stage = GameStage.Day }] };
    PlayerManager.Local = new() { Uid = host ? 1 : 2 };
    PlayerManager.Peers = new() { [host ? 2 : 1] = new() { Uid = host ? 2 : 1 } };
    GameFlow.LocalScene = Scene.DayScene;
    GameFlow.Destination = DayDestination.None;
    GameFlow.InStory = false;
    DayDestinationManager.ResetSession();
    BusinessStart.Reset();
    DayDestinationConfirmMessage.Sent.Clear();
    BusinessStartMessage.Sent.Clear();
}
void Intent(DayDestination target, System.Action continuation)
{
    DayDestinationManager.Submit(target, continuation);
    DayDestinationManager.ReceiveIntent(2, DayDestinationManager.Round, target);
}

Setup();
int entered = 0;
var admission = new TaskCompletionSource();
GameSession.Admission = admission.Task;
Intent(DayDestination.Business, () => entered++);
Check(DayDestinationConfirmMessage.Sent.Count == 0, "关闭入房尚未确认时不发出共同入口");
admission.SetResult();
PluginHost.Instance.Tick();
Check(DayDestinationConfirmMessage.Sent.Single().Target == DayDestination.Business, "入房关闭且意向一致后直接广播执行，无需客机再次接受");
PluginHost.Instance.Tick();
Check(entered == 1 && DayDestinationManager.Round == 2, "房主确认后执行一次原营业入口");
DayDestinationManager.ApplyConfirmation(1, DayDestination.Business);
DayDestinationManager.ReceiveIntent(2, 1, DayDestination.None);
DayDestinationManager.ReceiveIntent(2, 1, DayDestination.FinalTrialAgain);
PluginHost.Instance.Tick();
Check(entered == 1 && GameFlow.Destination == DayDestination.Business && DayDestinationConfirmMessage.Sent.Count == 1,
    "重复命令和迟到的撤回或改选不能取消或改变已确认营业");

Setup();
GameSession.Admission = new TaskCompletionSource().Task;
Intent(DayDestination.Business, () => entered++);
DayDestinationManager.ReceiveIntent(2, 1, DayDestination.None);
Check(DayDestinationManager.GetIntent(2) == DayDestination.None && DayDestinationManager.Round == 1
    && DayDestinationConfirmMessage.Sent.Count == 0, "房主确认前撤回只更新意向，不产生执行或取消命令");

Setup(false);
DayDestinationManager.Submit(DayDestination.Business, () => entered++);
int trialEntered = 0;
DayDestinationManager.Submit(DayDestination.FinalTrialAgain, () => trialEntered++);
DayDestinationManager.ApplyConfirmation(1, DayDestination.Business);
PluginHost.Instance.Tick();
Check(entered == 2 && trialEntered == 0 && GameFlow.Destination == DayDestination.Business,
    "客机收到房主命令前改选，仍执行已提交过的营业入口，不否决命令");
DayDestinationManager.ApplyConfirmation(1, DayDestination.Business);
PluginHost.Instance.Tick();
Check(entered == 2, "客机重复收到同轮执行命令不会重复执行");

Setup(false);
DayDestinationManager.Submit(DayDestination.FinalTrialAgain, () => entered++);
DayDestinationManager.ApplyConfirmation(1, DayDestination.FinalTrial);
PluginHost.Instance.Tick();
Check(MetaMystia.Patch.RunTimeSchedulerPatch.FirstTrialEntries == 1, "首次与重修混合时，重修客机沿已有首次客人入口进入");

Setup();
DayDestinationManager.Submit(DayDestination.Business, () => entered++);
GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Day }, new() { Uid = 2, Stage = GameStage.MainMenu }] };
DayDestinationManager.ReceiveIntent(2, 1, DayDestination.Business);
Check(DayDestinationConfirmMessage.Sent.Count == 0, "主菜单成员不被静默忽略或当成可营业成员");
GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Day }] };
PlayerManager.Peers.Clear();
DayDestinationManager.OnPeerLeft(2);
PluginHost.Instance.Tick();
Check(entered == 3, "未就绪成员离开后，房主可以继续已提交的入口");

Setup();
GameFlow.Destination = DayDestination.Business;
GameFlow.LocalScene = Scene.WorkScene;
int started = 0;
Check(BusinessStart.IsWaitingForStart, "普通营业初始化完成前已阻止厨具交互");
BusinessStart.Wait(() => started++);
BusinessStart.ReceiveReady(2);
BusinessStart.TryStart();
Check(started == 0, "营业开场同时等待本地回调与对端场景就绪");
Check(BusinessStart.IsWaitingForStart, "等待全员就绪期间保持厨具交互限制");
GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Work }, new() { Uid = 2, Stage = GameStage.Work }] };
BusinessStart.TryStart();
BusinessStart.TryStart();
Check(started == 1 && BusinessStartMessage.Sent.Count == 1, "全部开场就绪后只广播并执行一次营业放行");
Check(!BusinessStart.IsWaitingForStart, "主机放行后恢复厨具交互");

Setup();
GameSession.IsRoomHost = false;
GameFlow.Destination = DayDestination.Business;
BusinessStart.Wait(() => { });
Check(BusinessStart.IsWaitingForStart, "客机上报就绪后仍等待主机放行");
BusinessStart.ApplyStart();
Check(!BusinessStart.IsWaitingForStart, "客机收到开场命令后解除限制");
BusinessStart.Reset();
GameSession.IsInRoom = false;
Check(!BusinessStart.IsWaitingForStart, "断线后不限制单机厨具交互");
GameSession.IsInRoom = true;
GameFlow.Destination = DayDestination.FinalTrial;
Check(!BusinessStart.IsWaitingForStart, "最终试炼不使用普通营业交互限制");

Setup();
GameFlow.Destination = DayDestination.Business;
GameFlow.LocalScene = Scene.WorkScene;
BusinessStart.Wait(() => started++);
BusinessStart.Reset();
BusinessStart.TryStart();
Check(started == 1, "读档中断清除营业续接，不执行旧入口");
BusinessStart.Wait(() => started++);
BusinessStart.Reset(resume: true);
Check(started == 2, "普通断线可以解除开场等待并继续本地原流程");

Setup();
GameFlow.LocalScene = Scene.ResultScene;
DayDestinationManager.ReceiveIntent(2, 1, DayDestination.Business);
Check(DayDestinationManager.GetIntent(2) == DayDestination.Business && DayDestinationConfirmMessage.Sent.Count == 0,
    "房主收尾期间保留较快成员的新意向，但不能放行");
GameFlow.LocalScene = Scene.DayScene;
GameSession.Admission = new TaskCompletionSource().Task;
DayDestinationManager.Submit(DayDestination.Business, () => entered++);
DayDestinationManager.WithdrawLocal();
Check(DayDestinationManager.GetIntent(2) == DayDestination.Business && DayDestinationManager.GetIntent(1) == DayDestination.None,
    "自由读档只撤回本人意向，不清除其他成员的意向");

foreach (var (from, to) in new[]
{
    (Scene.MainScene, Scene.DayScene), (Scene.DayScene, Scene.DayScene), (Scene.DayScene, Scene.MainScene),
    (Scene.MainScene, Scene.MainScene), (Scene.DayScene, Scene.ResultScene), (Scene.ResultScene, Scene.DayScene),
}) Check(GameFlow.CanKeepRoom(from, to, DayDestination.None, false, false), $"自由转换保留房间：{from} → {to}");

foreach (var (from, to) in new[]
{
    (Scene.DayScene, Scene.IzakayaPrepScene), (Scene.IzakayaPrepScene, Scene.WorkScene),
    (Scene.WorkScene, Scene.ResultScene), (Scene.ResultScene, Scene.DayScene),
}) Check(GameFlow.CanKeepRoom(from, to, DayDestination.Business, false, false), $"常规营业保留房间：{from} → {to}");

foreach (var destination in new[] { DayDestination.FinalTrial, DayDestination.FinalTrialAgain })
{
    Check(GameFlow.CanKeepRoom(Scene.DayScene, Scene.WorkScene, destination, true, false), $"已确认试炼入口保留房间：{destination}");
    Check(GameFlow.CanKeepRoom(Scene.WorkScene, Scene.DayScene, destination, false, true), $"试炼正常返回保留房间：{destination}");
    Check(!GameFlow.CanKeepRoom(Scene.WorkScene, Scene.DayScene, destination, false, false), $"试炼非正常返回退房：{destination}");
}

Check(!GameFlow.CanKeepRoom(Scene.DayScene, Scene.WorkScene, DayDestination.None, false, false), "未接入同步的夜间入口退房");
Check(!GameFlow.CanKeepRoom(Scene.DayScene, Scene.WorkScene, DayDestination.FinalTrial, false, false), "试炼不能绕过已确认的入口");
foreach (var from in new[] { Scene.DayScene, Scene.IzakayaPrepScene, Scene.WorkScene })
{
    Check(!GameFlow.CanKeepRoom(from, Scene.MainScene, DayDestination.Business, false, false), $"共同阶段回菜单退房：{from}");
    Check(!GameFlow.CanKeepRoom(from, Scene.DayScene, DayDestination.Business, false, false), $"共同阶段直接重载白天退房：{from}");
}
Check(!GameFlow.CanKeepRoom(Scene.ResultScene, Scene.StaffScene, DayDestination.Business, false, false), "共同流程中未接入的结局改道退房");
Setup();
int immediateEntries = 0;
Intent(DayDestination.Business, () => immediateEntries++);
Check(immediateEntries == 1, "条件齐备时在当前调用中直接执行入口，不等下一帧");

Setup(false);
int storyEntries = 0;
bool replaying = false;
DayDestinationManager.Submit(DayDestination.Business, () =>
{
    storyEntries++;
    replaying = DayDestinationManager.ReplayingBusiness;
    DayDestinationManager.ContinueEntry();
});
GameFlow.InStory = true;
DayDestinationManager.ApplyConfirmation(1, DayDestination.Business);
Check(storyEntries == 0, "剧情未结束时保留入口");
GameFlow.InStory = false;
DayDestinationManager.ContinueEntry();
DayDestinationManager.ContinueEntry();
Check(storyEntries == 1 && replaying && !DayDestinationManager.ReplayingBusiness,
    "剧情结束后只续接一次，执行前取走回调，正常返回后清除重放标记");

Setup(false);
DayDestinationManager.Submit(DayDestination.Business, () => storyEntries++);
GameFlow.InStory = true;
DayDestinationManager.ApplyConfirmation(1, DayDestination.Business);
DayDestinationManager.ResetSession();
GameFlow.InStory = false;
DayDestinationManager.ContinueEntry();
Check(storyEntries == 1, "重置后剧情结束不能执行旧入口");

Setup();
GameSession.Admission = new TaskCompletionSource().Task;
Intent(DayDestination.Business, () => immediateEntries++);
var oldAdmissionCompleted = GameSession.AdmissionCompleted;
DayDestinationManager.WithdrawLocal();
oldAdmissionCompleted(true);
Check(immediateEntries == 1 && DayDestinationConfirmMessage.Sent.Count == 0,
    "撤回后迟到的关闭入房回调不能确认旧入口");

Setup();
GameSession.Admission = Task.FromException(new InvalidOperationException());
Intent(DayDestination.Business, () => immediateEntries++);
Check(immediateEntries == 1 && DayDestinationConfirmMessage.Sent.Count == 0,
    "关闭入房失败时不执行入口");
IzakayaSelectionChecks.Run(Check);

foreach (bool host in new[] { true, false })
{
    Setup(host);
    GameFlow.Destination = DayDestination.Business;
    GameFlow.LocalScene = Scene.WorkScene;
    int continued = 0;
    BusinessStart.Wait(() => continued++);
    GameFlow.LeaveRoomForTransition();
    BusinessStart.TryStart();
    Check(GameSession.Leaves == 1 && GameSession.Stops == 0 && !GameSession.Resumed,
        $"中断共同流程只请求退房并禁止续接，房主={host}");
    Check(continued == 0 && GameFlow.Destination == DayDestination.None,
        "退房中断清除旧营业回调与玩法状态");
}
Setup();
GameSession.IsInRoom = false;
GameFlow.LeaveRoomForTransition();
Check(GameSession.Leaves == 0 && GameSession.Stops == 0, "独立服务器世界玩家切场景保留连接");
GameSession.IsConnecting = true;
GameFlow.LeaveRoomForTransition();
Check(GameSession.Stops == 1 && !GameSession.Resumed, "场景中断取消尚未完成的连接");
Console.WriteLine($"ALL PASS ({checks} assertions; game and transport ports simulated)");

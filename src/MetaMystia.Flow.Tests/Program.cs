using Common.UI;

using MetaMystia;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
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
    GameSession.IsRoomHost = host;
    GameSession.Client = new();
    GameSession.Admission = Task.CompletedTask;
    GameSession.Room = new() { Host = 1, Members = [new() { Uid = 1, Stage = GameStage.Day }, new() { Uid = 2, Stage = GameStage.Day }] };
    PlayerManager.Local = new() { Uid = host ? 1 : 2 };
    PlayerManager.Peers = new() { [host ? 2 : 1] = new() { Uid = host ? 2 : 1 } };
    GameFlow.LocalScene = Scene.DayScene;
    GameFlow.Destination = DayDestination.None;
    GameFlow.InStory = false;
    DayDestinationManager.ResetSession();
    BusinessStart.Reset();
    DayDestinationConfirmAction.Sent.Clear();
    DayEntryReplyAction.Sent.Clear();
    BusinessStartAction.Sent.Clear();
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
Check(DayDestinationConfirmAction.Sent.Count == 0, "关闭入房尚未确认时不发出共同入口");
admission.SetResult();
PluginHost.Instance.Tick();
Check(entered == 0 && DayDestinationConfirmAction.Sent.Single().Step == EntryStep.Prepare, "入房关闭后仍等待每个成员接受入口");
DayDestinationManager.ReceiveEntryReply(2, 1, true);
PluginHost.Instance.Tick();
Check(entered == 1 && DayDestinationManager.Round == 2, "全员接受后只执行一次原营业入口");
DayDestinationManager.ReceiveEntryReply(2, 1, true);
PluginHost.Instance.Tick();
Check(entered == 1, "重复入口回复不会重复推进");

Setup();
Intent(DayDestination.Business, () => entered++);
DayDestinationManager.ReceiveEntryReply(2, 1, false);
PluginHost.Instance.Tick();
Check(entered == 1 && GameFlow.Destination == DayDestination.None, "读档玩家拒绝旧意向时取消放行，不执行游戏副作用");
Check(DayDestinationManager.GetIntent(2) == DayDestination.None && DayDestinationManager.Round == 2, "撤回者意向清除，已有入口编号推进");

Setup(false);
DayDestinationManager.Submit(DayDestination.Business, () => entered++);
DayDestinationManager.WithdrawLocal();
GameFlow.LocalScene = Scene.LoadScene;
DayDestinationManager.PrepareEntry(1, DayDestination.Business);
Check(DayEntryReplyAction.Sent.Single().Ready == false && GameFlow.Destination == DayDestination.None, "客机先读档、后收到入口提议时明确拒绝");
DayDestinationManager.CancelEntry(1);
Check(DayDestinationManager.Round == 2, "加载中的客机也接收取消，不与房主入口编号脱节");

Setup(false);
DayDestinationManager.Submit(DayDestination.FinalTrialAgain, () => entered++);
DayDestinationManager.PrepareEntry(1, DayDestination.FinalTrial);
DayDestinationManager.ApplyConfirmation(1, DayDestination.FinalTrial);
PluginHost.Instance.Tick();
Check(MetaMystia.Patch.RunTimeSchedulerPatch.FirstTrialEntries == 1, "首次与重修混合时，重修客机沿已有首次客人入口进入");

Setup();
DayDestinationManager.Submit(DayDestination.Business, () => entered++);
GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Day }, new() { Uid = 2, Stage = GameStage.MainMenu }] };
DayDestinationManager.ReceiveIntent(2, 1, DayDestination.Business);
Check(DayDestinationConfirmAction.Sent.Count == 0, "主菜单成员不被静默忽略或当成可营业成员");
GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Day }] };
PlayerManager.Peers.Clear();
DayDestinationManager.OnPeerLeft(2);
PluginHost.Instance.Tick();
Check(entered == 2, "未就绪成员离开后，房主可以继续已提交的入口");

Setup();
GameFlow.Destination = DayDestination.Business;
GameFlow.LocalScene = Scene.WorkScene;
int started = 0;
BusinessStart.Wait(() => started++);
BusinessStart.ReceiveReady(2);
BusinessStart.TryStart();
Check(started == 0, "营业开场同时等待本地回调与对端场景就绪");
GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1, Stage = GameStage.Work }, new() { Uid = 2, Stage = GameStage.Work }] };
BusinessStart.TryStart();
BusinessStart.TryStart();
Check(started == 1 && BusinessStartAction.Sent.Count == 1, "全部开场就绪后只广播并执行一次营业放行");

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
Check(DayDestinationManager.GetIntent(2) == DayDestination.Business && DayDestinationConfirmAction.Sent.Count == 0,
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
}) Check(GameFlow.CanKeepConnection(from, to, DayDestination.None, false, false), $"自由转换保留连接：{from} → {to}");

foreach (var (from, to) in new[]
{
    (Scene.DayScene, Scene.IzakayaPrepScene), (Scene.IzakayaPrepScene, Scene.WorkScene),
    (Scene.WorkScene, Scene.ResultScene), (Scene.ResultScene, Scene.DayScene),
}) Check(GameFlow.CanKeepConnection(from, to, DayDestination.Business, false, false), $"常规营业保留连接：{from} → {to}");

foreach (var destination in new[] { DayDestination.FinalTrial, DayDestination.FinalTrialAgain })
{
    Check(GameFlow.CanKeepConnection(Scene.DayScene, Scene.WorkScene, destination, true, false), $"已确认试炼入口保留连接：{destination}");
    Check(GameFlow.CanKeepConnection(Scene.WorkScene, Scene.DayScene, destination, false, true), $"试炼正常返回保留连接：{destination}");
    Check(!GameFlow.CanKeepConnection(Scene.WorkScene, Scene.DayScene, destination, false, false), $"试炼非正常返回断开：{destination}");
}

Check(!GameFlow.CanKeepConnection(Scene.DayScene, Scene.WorkScene, DayDestination.None, false, false), "未接入同步的夜间入口断开");
Check(!GameFlow.CanKeepConnection(Scene.DayScene, Scene.WorkScene, DayDestination.FinalTrial, false, false), "试炼不能绕过已确认的入口");
foreach (var from in new[] { Scene.DayScene, Scene.IzakayaPrepScene, Scene.WorkScene })
{
    Check(!GameFlow.CanKeepConnection(from, Scene.MainScene, DayDestination.Business, false, false), $"共同阶段回菜单断开：{from}");
    Check(!GameFlow.CanKeepConnection(from, Scene.DayScene, DayDestination.Business, false, false), $"共同阶段直接重载白天断开：{from}");
}
Check(!GameFlow.CanKeepConnection(Scene.ResultScene, Scene.StaffScene, DayDestination.Business, false, false), "共同流程中未接入的结局改道断开");
Console.WriteLine($"ALL PASS ({checks} assertions; game and transport ports simulated)");

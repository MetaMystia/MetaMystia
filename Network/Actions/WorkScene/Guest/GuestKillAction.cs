using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机 FSM 异常 (FallBack) 时广播的强制清理信号。
/// 客机收到后调用 GuestFSM.DoKill -> GuestReplayService.ReplayForceCleanupGuest 释放
/// 桌位 / 顾客图标 / 可赶客注册 / 耐心倒计时 / 桌面 sprite 等全局状态，并把 FSM 推到 Dead。
///
/// 仅由主机 FallBack 路径发出；客机 FallBack 不广播 (客机异常通常源于自身与主机不同步，
/// 不应反向污染主机权威)。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestKillAction : Action
{

    public int RuntimeId { get; set; }
    public GuestFSM.State HostStateBeforeKill { get; set; }   // 调试用：观测主客状态分歧
    public int DeskCode { get; set; } = -1;

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var deskCode = DeskCode;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null)
        {
            GuestService.CleanGuestOrderRegistrationForDesk(deskCode);
            return;
        }

        Log.Error($"Guest #{RuntimeId} is being killed by host (host was {HostStateBeforeKill}, client was {fsm.CurrentState})");
        fsm.Kill();
    }

    public static void Send(int runtimeId, GuestFSM.State hostStateBeforeKill, int deskCode) =>
        new GuestKillAction
        {
            RuntimeId = runtimeId,
            HostStateBeforeKill = hostStateBeforeKill,
            DeskCode = deskCode
        }.Enqueue();
}

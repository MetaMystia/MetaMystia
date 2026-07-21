using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 主机判定排队耐心耗尽：
/// 调用栈: GuestGroupController.UpdatePatient (CurrentPatient<=0)
///       -> OnPatientDepeletedCallback (= PostInitializeGuestGroup 内闭包 OnPatientDepleted)
///       -> GuestsManager.RemoveFromPatientCountdown
///       -> GuestGroupController.MoveToSpawn
/// 与桌上耐心耗尽不同：不付款、不清订单、不关面板、绕过 LeaveFromDesk。
/// 客机重放仅做 RemoveFromPatientCountdown + MoveToSpawn 这条最小副作用。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class PatientDepletedQueueAction : Action
{

    public int RuntimeId { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoPatientDepletedInQueue),
            () => GuestFSM.DoPatientDepletedInQueue(rid));
    }

    public static void Send(int runtimeId) =>
        new PatientDepletedQueueAction { RuntimeId = runtimeId }.Enqueue();
}

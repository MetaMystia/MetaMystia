using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

/// <summary>
/// 主机判定桌上耐心耗尽：
/// 调用栈: GuestGroupController.UpdatePatient (CurrentPatient<=0)
///       -> OnPatientDepeletedCallback (= GuestsManager.PatientDepletedLeave)
///       -> EventManager.LoseAllCombo
///       -> RemoveFromPatientCountdown
///       -> OnPatienceRunOutCallback
///       -> onOrderRemove(PeekOrders) + registeredCharacterArrivedEvents.Remove(DeskCode)
///       -> onForcePannelClosingWhenGuestRepellCallback (若匹配)
///       -> GuestPay(toLeave, includeTip: true)
///       -> LeaveFromDesk(toLeave)
/// 客机通过一次性放行调用原版 PatientDepletedLeave，完整执行清理与离桌。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class PatientDepletedDeskAction : Action
{

    public int RuntimeId { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        QueueForGuest(fsm, nameof(GuestFSM.DoPatientDepletedAtDesk),
            () => GuestFSM.DoPatientDepletedAtDesk(rid));
    }

    public static void Send(int runtimeId) =>
        new PatientDepletedDeskAction { RuntimeId = runtimeId }.Enqueue();
}

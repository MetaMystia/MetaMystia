using MemoryPack;

namespace MetaMystia.Network;

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
/// 客机重放需要等价地推进副作用，但 PatientDepletedLeave 是 private，所以由
/// GuestReplayService.ReplayPatientDepletedLeave 复刻。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class PatientDepletedDeskAction : Action
{
    public override ActionType Type => ActionType.PatientDepletedDeskAction;

    public int RuntimeId { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (MpManager.IsConnectedHost) return;

        var rid = RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoPatientDepletedAtDesk),
                () => GuestFSM.DoPatientDepletedAtDesk(rid));
        });
    }

    public static void Send(int runtimeId)
    {
        var action = new PatientDepletedDeskAction
        {
            RuntimeId = runtimeId
        };
        action.SendToHostOrBroadcast();
    }
}

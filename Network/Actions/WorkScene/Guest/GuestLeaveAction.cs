using MemoryPack;

using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

/// <summary>
/// 主机权威：顾客离桌主链 (FSM: * → Leaving → Left)。
/// 调用栈覆盖：GenerateOrderSession 失败 4 分支 / PatientDepletedLeave / ExBadLeave / SetManualControlledLeave /
///            RepellInternal / PayAndLeave (ExceedEndurance 协程末端)。
/// 客机收到后 Grant <see cref="MetaMystia.Patch.GuestsManagerPatch.SkipLeaveFromDeskPatch"/> 放权一次,
/// 调用本地 LeaveFromDesk 让原游戏代码自然完成 occupiedDesks 清理 / CleanDesk / OnLeaveDeskCallback /
/// CheckAndSendFromQueue / FinalLeave (MoveToSpawn / FlyToSpawn) 等所有副作用。
/// triggerLeaveBuff 客机端强制 false 以避免 Special 顾客的负面 buff 在双端各触发一次。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestLeaveAction : Action
{

    public int RuntimeId { get; set; }
    public byte LeaveType { get; set; }
    public bool TriggerLeaveBuff { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var leaveType = (GuestGroupController.LeaveType)LeaveType;
        var triggerLeaveBuff = TriggerLeaveBuff;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoLeaveFromDesk),
            () => GuestFSM.DoLeaveFromDesk(rid, leaveType, triggerLeaveBuff));
    }

    public static void Send(int runtimeId, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff) =>
        new GuestLeaveAction
        {
            RuntimeId = runtimeId,
            LeaveType = (byte)leaveType,
            TriggerLeaveBuff = triggerLeaveBuff
        }.Enqueue();
}

using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class GuestLeaveBehavior
{
    public static void Send(int runtimeId, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff) =>
        new GuestLeaveAction
        {
            RuntimeId = runtimeId,
            LeaveType = leaveType.ToWire(),
            TriggerLeaveBuff = triggerLeaveBuff
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<GuestLeaveAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(GuestLeaveAction action)
    {
        var rid = action.RuntimeId;
        var leaveType = action.LeaveType.ToGameLeaveType();
        var triggerLeaveBuff = action.TriggerLeaveBuff;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoLeaveFromDesk),
            () => GuestFSM.DoLeaveFromDesk(rid, leaveType, triggerLeaveBuff));
    }
}

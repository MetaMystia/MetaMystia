using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class MoveToQueueAction : Action
{
    public override ActionType Type => ActionType.MoveToQueueAction;

    public int RuntimeId { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoMoveToQueue),
                () => GuestFSM.DoMoveToQueue(rid));
        });
    }

    public static void Send(int runtimeId)
    {
        var action = new MoveToQueueAction()
        {
            RuntimeId = runtimeId
        };
        action.SendToHostOrBroadcast();
    }
}

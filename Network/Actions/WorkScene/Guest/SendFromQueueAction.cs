using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class SendFromQueueAction : Action
{
    public override ActionType Type => ActionType.SendFromQueueAction;
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
            fsm.Enqueue(nameof(GuestFSM.DoSendFromQueue),
                () => GuestFSM.DoSendFromQueue(rid));
        });
    }

    public static void Send(int runtimeId)
    {
        var action = new SendFromQueueAction
        {
            RuntimeId = runtimeId,
        };
        action.SendToHostOrBroadcast();
    }
}

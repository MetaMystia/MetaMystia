using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class MoveToDeskAction : Action
{

    public int RuntimeId { get; set; }
    public int DeskCode { get; set; }


    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var deskCode = DeskCode;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoMoveToDesk),
            () => GuestFSM.DoMoveToDesk(rid, deskCode));
    }

    public static void Send(int runtimeId, int deskCode) =>
        new MoveToDeskAction { RuntimeId = runtimeId, DeskCode = deskCode }.Enqueue();
}

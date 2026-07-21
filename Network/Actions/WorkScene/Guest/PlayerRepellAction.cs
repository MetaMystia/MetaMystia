using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class PlayerRepellAction : Action
{

    public int RuntimeId { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoPlayerRepell),
            () => GuestFSM.DoPlayerRepell(rid));
    }

    public static void Send(int runtimeId) =>
        new PlayerRepellAction { RuntimeId = runtimeId }.Enqueue();
}

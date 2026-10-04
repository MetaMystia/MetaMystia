using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class PlayerRepellMessage : MultiplayerMessage
{

    public int RuntimeId { get; set; }

    protected override bool HostOnlyReceive => true;
    protected override bool DiscardOnStory => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        // 与其它顾客消息一致：重放排进 FSM 队列，在营业场景循环内执行。决定赶客要走 IWorkSceneGuests 服务，
        // 而收包线程不在场景服务作用域内。
        var rid = RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        QueueForGuest(fsm, nameof(GuestFSM.DoPlayerRepell), () =>
        {
            GuestFSM.DoPlayerRepell(rid);
            return true;
        });
    }

    public static void Send(int runtimeId) =>
        new PlayerRepellMessage { RuntimeId = runtimeId }.Enqueue();
}

using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class PlayerRepellAction : Action
{

    public int RuntimeId { get; set; }

    [HostOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        GuestFSM.DoPlayerRepell(RuntimeId);
    }

    public static void Send(int runtimeId) =>
        new PlayerRepellAction { RuntimeId = runtimeId }.Enqueue();
}

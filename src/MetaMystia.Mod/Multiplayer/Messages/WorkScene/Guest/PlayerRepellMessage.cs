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
        GuestFSM.DoPlayerRepell(RuntimeId);
    }

    public static void Send(int runtimeId) =>
        new PlayerRepellMessage { RuntimeId = runtimeId }.Enqueue();
}

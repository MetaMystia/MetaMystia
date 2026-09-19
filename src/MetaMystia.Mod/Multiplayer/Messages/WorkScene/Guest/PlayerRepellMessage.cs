using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class PlayerRepellMessage : MultiplayerMessage
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
        new PlayerRepellMessage { RuntimeId = runtimeId }.Enqueue();
}

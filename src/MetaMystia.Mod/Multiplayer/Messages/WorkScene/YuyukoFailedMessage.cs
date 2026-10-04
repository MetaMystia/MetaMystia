using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机确认幽幽子挑战失败，客机进入原版失败流程。</summary>
[MemoryPackable]
[AutoLog]
public partial class YuyukoFailedMessage : MultiplayerMessage
{
    protected override bool RequireHostSender => true;
    protected override bool ClientOnlyReceive => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived() => YuyukoGuestSync.ReceiveFailure();

    public static void Send() => new YuyukoFailedMessage().Enqueue();
}

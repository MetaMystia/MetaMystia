using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机确认幽幽子挑战失败，客机进入原版失败流程。</summary>
[MemoryPackable]
[AutoLog]
public partial class YuyukoFailedMessage : MultiplayerMessage
{
    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => YuyukoBossDataPatch.ReceiveFailure();

    public static void Send() => new YuyukoFailedMessage().Enqueue();
}

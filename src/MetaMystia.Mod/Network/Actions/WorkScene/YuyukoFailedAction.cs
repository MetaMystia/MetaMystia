using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>主机确认幽幽子挑战失败，客机进入原版失败流程。</summary>
[MemoryPackable]
[AutoLog]
public partial class YuyukoFailedAction : Action
{
    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => YuyukoFailurePatch.ReceiveFailure();

    public static void Send() => new YuyukoFailedAction().Enqueue();
}

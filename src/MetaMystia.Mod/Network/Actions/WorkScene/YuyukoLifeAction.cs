using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>主机同步三阶段剩余生命，同时驱动客机挑战状态和显示。</summary>
[MemoryPackable]
[AutoLog]
public partial class YuyukoLifeAction : Action
{
    public int Life { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => IncomeControllerYuyukoPatch.ReceiveProgress(Life);

    public static void Send(int life) => new YuyukoLifeAction { Life = life }.Enqueue();
}

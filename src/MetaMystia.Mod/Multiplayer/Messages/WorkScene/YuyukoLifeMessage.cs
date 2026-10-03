using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机同步三阶段剩余生命，同时驱动客机挑战状态和显示。</summary>
[MemoryPackable]
[AutoLog]
public partial class YuyukoLifeMessage : MultiplayerMessage
{
    public int Life { get; set; }

    protected override bool RequireHostSender => true;
    protected override bool ClientOnlyReceive => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived() => YuyukoGuestSync.ReceiveLife(Life);

    public static void Send(int life) => new YuyukoLifeMessage { Life = life }.Enqueue();
}

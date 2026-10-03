using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Listeners;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.ExpEdit 的网络同步
/// </summary>
[MemoryPackable]
[AutoLog]

public partial class ExpEditMessage : MultiplayerMessage
{

    public float Value { get; set; }
    public EventManager.MathOperation MathOp { get; set; }

    protected override bool ClientOnlyReceive => true;
    protected override bool DiscardOnStory => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        // 服务只在营业场景循环作用域内可用，编辑排到 GuestSync 的重放队列里执行。
        GuestSync.EnqueueReplay("exp edit", services => MetricsSync.ReplayExperience(services, Value, MathOp));
    }

    public static void Send(float value, EventManager.MathOperation mathOp) =>
        new ExpEditMessage { Value = value, MathOp = mathOp }.Enqueue();
}

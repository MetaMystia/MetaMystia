using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Listeners;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.PassionEdit 的网络同步
/// </summary>
[MemoryPackable]
[AutoLog]

public partial class PassionEditMessage : MultiplayerMessage
{

    public float Value { get; set; }
    public EventManager.MathOperation MathOp { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        // 服务只在营业场景循环作用域内可用，编辑排到 GuestSync 的重放队列里执行。
        GuestSync.EnqueueReplay("passion edit", services => MetricsSync.ReplayPassion(services, Value, MathOp));
    }

    public static void Send(float value, EventManager.MathOperation mathOp) =>
        new PassionEditMessage { Value = value, MathOp = mathOp }.Enqueue();
}

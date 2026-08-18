using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.FundEdit 的网络同步
/// </summary>
[MemoryPackable]
[AutoLog]

public partial class FundEditAction : Action
{

    public float Value { get; set; }
    public EventManager.MathOperation MathOp { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var em = EventManager.Instance;
        if (em == null) return;
        NightSceneEventManagerPatch.FundEdit_ReversePatch(em, Value, MathOp);
    }

    public static void Send(float value, EventManager.MathOperation mathOp) =>
        new FundEditAction { Value = value, MathOp = mathOp }.Enqueue();
}

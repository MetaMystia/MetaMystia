using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.ExpEdit 的网络同步
/// </summary>
[MemoryPackable]
[AutoLog]

public partial class ExpEditAction : Action
{
    public override ActionType Type => ActionType.ExpEditAction;

    public float Value { get; set; }
    public EventManager.MathOperation MathOp { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (MpManager.IsConnectedHost) return;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var em = EventManager.Instance;
            if (em == null) return;
            NightSceneEventManagerPatch.ExpEdit_ReversePatch(em, Value, MathOp);
        });
    }

    public static void Send(float value, EventManager.MathOperation mathOp)
    {
        new ExpEditAction
        {
            Value = value,
            MathOp = mathOp
        }.SendToHostOrBroadcast();
    }
}
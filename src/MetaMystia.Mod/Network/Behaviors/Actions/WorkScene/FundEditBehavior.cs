using MetaMystia.Patch;
using NightScene.EventUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class FundEditBehavior
{
    public static void Send(float value, EventManager.MathOperation mathOp) =>
        new FundEditAction { Value = value, MathOp = mathOp.ToWire() }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<FundEditAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(FundEditAction action)
    {
        var em = EventManager.Instance;
        if (em == null) return;
        NightSceneEventManagerPatch.FundEdit_ReversePatch(em, action.Value, action.MathOp.ToGameMathOperation());
    }
}

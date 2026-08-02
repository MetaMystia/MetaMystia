using MetaMystia.Patch;
using NightScene.EventUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ExpEditBehavior
{
    public static void Send(float value, EventManager.MathOperation mathOp) =>
        new ExpEditAction { Value = value, MathOp = mathOp.ToWire() }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ExpEditAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(ExpEditAction action)
    {
        var em = EventManager.Instance;
        if (em == null) return;
        NightSceneEventManagerPatch.ExpEdit_ReversePatch(em, action.Value, action.MathOp.ToGameMathOperation());
    }
}

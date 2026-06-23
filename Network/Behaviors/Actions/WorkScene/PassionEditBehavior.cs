using MetaMystia.Patch;
using NightScene.EventUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PassionEditBehavior
{
    public static void Send(float value, EventManager.MathOperation mathOp) =>
        new PassionEditAction { Value = value, MathOp = mathOp.ToWire() }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PassionEditAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(PassionEditAction action)
    {
        var em = EventManager.Instance;
        if (em == null) return;
        NightSceneEventManagerPatch.PassionEdit_ReversePatch(em, action.Value, action.MathOp.ToGameMathOperation());
    }
}

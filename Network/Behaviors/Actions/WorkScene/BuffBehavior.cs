using MetaMystia.Patch;
using SgrYuki;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class BuffBehavior
{
    public static void Send(QTEBuff buff)
    {
        new BuffAction { Buff = buff }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<BuffAction>(Handle,
            scene: Common.UI.Scene.WorkScene);
    }

    private static void Handle(BuffAction action)
    {
        CommandScheduler.Enqueue(
            executeWhen: () => !QTERewardManagerPatch.OnQTESucceededExecuting,
            executeInfo: "BuffAction OnQTESucceededExecuting",
            execute: () =>
            {
                QTERewardManagerPatch.BuffLocalTrigger = false;
                QTERewardManagerPatch.OnQTESucceeded(
                    NightScene.CookingUtility.QTERewardManager.Instance,
                    action.Buff.ID,
                    true);
                QTERewardManagerPatch.BuffLocalTrigger = true;
                Plugin.Instance?.Log.LogMessage($"triggered buff {action.Buff}");
            },
            timeoutSeconds: 10f);
    }
}

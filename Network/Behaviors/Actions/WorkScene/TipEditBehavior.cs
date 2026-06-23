using MetaMystia.Patch;
using NightScene.EventUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class TipEditBehavior
{
    public static void Send(int value, EventManager.ServeType serveType, float comboBuff, float moodBuff, float extraBuff) =>
        new TipEditAction
        {
            IntValue = value,
            ServeType = serveType.ToWire(),
            ComboBuff = comboBuff,
            MoodBuff = moodBuff,
            ExtraBuff = extraBuff
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<TipEditAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(TipEditAction action)
    {
        var em = EventManager.Instance;
        if (em == null) return;
        NightSceneEventManagerPatch.TipEdit_ReversePatch(
            em,
            action.IntValue,
            action.ServeType.ToGameServeType(),
            action.ComboBuff,
            action.MoodBuff,
            action.ExtraBuff);
    }
}

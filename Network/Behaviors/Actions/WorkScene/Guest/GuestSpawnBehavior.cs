namespace MetaMystia.Network;

[NetActionBehavior]
internal static class GuestSpawnBehavior
{
    public static void Send(int runtimeId, GuestSpawnInfo spawnInfo) =>
        new GuestSpawnAction { RuntimeId = runtimeId, SpawnInfo = spawnInfo }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<GuestSpawnAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(GuestSpawnAction action)
    {
        GuestFSM.DoSpawn(action.RuntimeId, action.SpawnInfo);
    }
}

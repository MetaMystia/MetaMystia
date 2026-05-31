using MemoryPack;

using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class GuestSpawnAction : Action
{
    public override ActionType Type => ActionType.GuestSpawnAction;

    public int RuntimeId { get; set; }
    public GuestSpawnInfo SpawnInfo { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        PluginManager.Instance.RunOnMainThread(() => GuestFSM.DoSpawn(RuntimeId, SpawnInfo));
    }

    public static void Send(int runtimeId, GuestSpawnInfo spawnInfo)
    {
        var action = new GuestSpawnAction()
        {
            RuntimeId = runtimeId,
            SpawnInfo = spawnInfo,
        };
        action.SendToHostOrBroadcast();
    }
}

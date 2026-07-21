using MemoryPack;

using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class GuestSpawnAction : Action
{

    public int RuntimeId { get; set; }
    public GuestSpawnInfo SpawnInfo { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        GuestFSM.DoSpawn(RuntimeId, SpawnInfo);
    }

    public static void Send(int runtimeId, GuestSpawnInfo spawnInfo) =>
        new GuestSpawnAction { RuntimeId = runtimeId, SpawnInfo = spawnInfo }.Enqueue();
}

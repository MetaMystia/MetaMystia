using MemoryPack;

using NightScene.GuestManagementUtility;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class GuestSpawnMessage : MultiplayerMessage
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
        new GuestSpawnMessage { RuntimeId = runtimeId, SpawnInfo = spawnInfo }.Enqueue();
}

using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public sealed partial class ResourceSnapshotAction : Action
{
    public ResourceManifest Manifest { get; set; }
    public override void OnReceivedDerived()
    {
        if (!ResourceDataBase.ValidManifest(Manifest)) { RoomGameplay.Abort("Invalid resource snapshot"); return; }
        ModPlayerStore.ApplyResources(SenderUid, Manifest);
    }
    public static void Send()
    {
        if (PlayerManager.Local.DataBase.IsLoaded)
            new ResourceSnapshotAction { Manifest = PlayerManager.Local.DataBase.ToManifest() }.Enqueue();
    }
}

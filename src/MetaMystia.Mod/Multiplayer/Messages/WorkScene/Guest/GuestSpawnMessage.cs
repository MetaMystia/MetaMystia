using System.Linq;

using Il2CppSystem.Linq;
using MemoryPack;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机：顾客生成（PostInitializeGuestGroup 入口）。客机构造控制器，去向等待入座或排队事件。</summary>
[MemoryPackable]
public partial class GuestSpawnMessage : MultiplayerMessage, IGuestEvent
{
    public int RuntimeId { get; set; }
    public GuestSpawnInfo SpawnInfo { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var registry = GuestRegistry.Current;
        if (registry.Find(RuntimeId) != null) return;
        registry.Add(RuntimeId, GuestTrack.Replicated, SpawnInfo);
        registry.Post(this);
    }

    public bool Apply(GuestRecord guest, bool force)
    {
        if (guest.Controller == null) GuestReplay.Spawn(guest);
        return true;
    }

    public static void Detect(GuestGroupController controller, GuestsManagerPatch.PendingSpawnArgs? spawnArgs)
    {
        var info = new GuestSpawnInfo
        {
            GuestType = controller.ControllType,
            Ids = controller.GetAllGuests().ToArray().Select(guest => guest.Id).ToArray(),
            Fund = controller.GetFund,
            MaxFundCarry = controller.MaxFundCarry,
        };
        if (spawnArgs is { } args)
        {
            info.HasNormalSpawnArgs = info.GuestType == GuestsManager.GuestType.Normal;
            info.HasSpecialSpawnArgs = info.GuestType == GuestsManager.GuestType.Special;
            info.GuestSpawnType = args.GuestSpawnType;
            info.HasOverrideSpawnPosition = args.HasOverrideSpawnPosition;
            info.OverrideSpawnX = args.OverrideSpawnPosition.x;
            info.OverrideSpawnY = args.OverrideSpawnPosition.y;
            info.OverrideSpawnZ = args.OverrideSpawnPosition.z;
            info.LeaveType = args.LeaveType;
            info.TargetDeskCode = args.TargetDeskCode;
            info.ShouldFade = args.ShouldFade;
        }
        var guest = GuestRegistry.Current.Add(0, GuestTrack.Replicated, info, controller);
        new GuestSpawnMessage { RuntimeId = guest.RuntimeId, SpawnInfo = info }.Enqueue();
    }
}

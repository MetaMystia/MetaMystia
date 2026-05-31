using MemoryPack;

using NightScene.GuestManagementUtility;

using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class GuestSpawnInfo
{
    public GuestType GuestType { get; set; }
    public int[] Ids { get; set; }
    public int Fund { get; set; }
    public int MaxFundCarry { get; set; }
    public bool HasNormalSpawnArgs { get; set; }
    public bool HasOverrideSpawnPosition { get; set; }
    public float OverrideSpawnX { get; set; }
    public float OverrideSpawnY { get; set; }
    public float OverrideSpawnZ { get; set; }
    public GuestGroupController.LeaveType LeaveType { get; set; } = GuestGroupController.LeaveType.Move;
    public int TargetDeskCode { get; set; } = -1;
    public bool ShouldFade { get; set; } = true;
}

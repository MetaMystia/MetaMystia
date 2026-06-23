using MemoryPack;

namespace MetaMystia.Network;

// 纯数据 DTO（协议层）：游戏枚举以 Wire* 镜像，零游戏依赖。
// 构造/读取处（GuestFSM / GuestService）在 mod 边界用 WireEnumMaps 互转。

[MemoryPackable]
public partial class GuestSpawnInfo
{
    public WireGuestType GuestType { get; set; }
    public int[] Ids { get; set; }
    public int Fund { get; set; }
    public int MaxFundCarry { get; set; }
    public bool HasNormalSpawnArgs { get; set; }
    public bool HasOverrideSpawnPosition { get; set; }
    public float OverrideSpawnX { get; set; }
    public float OverrideSpawnY { get; set; }
    public float OverrideSpawnZ { get; set; }
    public WireLeaveType LeaveType { get; set; } = WireLeaveType.Move;
    public int TargetDeskCode { get; set; } = -1;
    public bool ShouldFade { get; set; } = true;
}

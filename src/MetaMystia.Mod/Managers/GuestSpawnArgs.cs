using Mystia.Listeners;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia;

/// <summary>
/// 主机捕获到的顾客生成参数（原 <c>GuestsManagerPatch.PendingSpawnArgs</c>）。
/// 由 <c>IGuestSpawnModifier.OnPreSpawnNormalGuests</c>/<c>OnPreSpawnSpecialGuest</c> 在生成时暂存，
/// 随 <c>GuestSpawnMessage</c> 广播给客机。
/// </summary>
public readonly struct PendingSpawnArgs
{
    public bool HasOverrideSpawnPosition { get; init; }

    public Vector3 OverrideSpawnPosition { get; init; }

    public GuestGroupController.LeaveType LeaveType { get; init; }

    public int TargetDeskCode { get; init; }

    public bool ShouldFade { get; init; }

    public SpecialGuestsController.GuestSpawnType GuestSpawnType { get; init; }

    /// <summary>
    /// 由框架的生成请求构造。生成请求不含稀客生成类型（原补丁取的是
    /// <c>SpawnSpecialGuestGroup</c> 的形参），因此沿用原版默认值。
    /// </summary>
    public static PendingSpawnArgs FromRequest(GuestSpawnRequest request)
        => new()
        {
            HasOverrideSpawnPosition = request.SpawnPosition.HasValue,
            OverrideSpawnPosition = request.SpawnPosition.GetValueOrDefault(),
            LeaveType = request.LeaveType,
            TargetDeskCode = request.DeskCode,
            ShouldFade = request.Fade,
            GuestSpawnType = SpecialGuestsController.GuestSpawnType.Normal,
        };
}

using HarmonyLib;

using NightScene.GuestManagementUtility;

using SgrYuki.Utils;

namespace MetaMystia.Patch;

/// <summary>
/// 修复 GuestIconManager.SwitchState 在 controller 为 null 时触发 NRE 的问题。
/// 神绮黑卡驱逐客人后，controller 可能已被清理但图标切换仍被调用。
/// </summary>
[HarmonyPatch(typeof(GuestIconManager), nameof(GuestIconManager.SwitchState))]
[AutoLog]
public partial class ShinkiGuestIconManagerPatch
{
    [HarmonyPrefix]
    public static bool SwitchState_Prefix(GuestGroupController guestGroupController, GuestState state)
    {
        return guestGroupController != null;
    }
}

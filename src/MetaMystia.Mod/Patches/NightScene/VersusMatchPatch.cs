using HarmonyLib;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

/// <summary>
/// 料理对决局内：旁观者可以走动，不能与夜间场景交互（库存面板的取出见 <see cref="WorkSceneStoragePannelPatch"/>）；
/// 房主的差评离桌与耐心耗尽按桌的归属队记付款与断连击（见 <see cref="VersusMatch.CreditScope"/>）。
/// </summary>
[HarmonyPatch]
public static class VersusMatchPatch
{
    [HarmonyPatch(typeof(NightScene.Input.WorkScenePlayerInputGenerator), "Interact")]
    [HarmonyPrefix]
    public static bool Interact_Prefix() => VersusMatch.IsSpectating ? SkipOriginal : RunOriginal;

    [HarmonyPatch(typeof(GuestsManager), nameof(GuestsManager.PayByMood))]
    [HarmonyPrefix]
    public static void PayByMood_Prefix(GuestGroupController toPayAndLeave, out VersusMatch.CreditScope __state) =>
        __state = VersusMatch.CreditDesk(toPayAndLeave.DeskCode);

    [HarmonyPatch(typeof(GuestsManager), nameof(GuestsManager.PayByMood))]
    [HarmonyPostfix]
    public static void PayByMood_Postfix(VersusMatch.CreditScope __state) => __state.Dispose();

    [HarmonyPatch(typeof(GuestsManager), nameof(GuestsManager.PatientDepletedLeave))]
    [HarmonyPrefix]
    public static void PatientDepletedLeave_Prefix(GuestGroupController toPatientDepletedLeave, out VersusMatch.CreditScope __state) =>
        __state = VersusMatch.CreditDesk(toPatientDepletedLeave.DeskCode);

    [HarmonyPatch(typeof(GuestsManager), nameof(GuestsManager.PatientDepletedLeave))]
    [HarmonyPostfix]
    public static void PatientDepletedLeave_Postfix(VersusMatch.CreditScope __state) => __state.Dispose();

    [HarmonyPatch(typeof(EventManager), nameof(EventManager.LoseAllCombo))]
    [HarmonyPrefix]
    public static bool LoseAllCombo_Prefix() => VersusMatch.RedirectLoseCombo() ? SkipOriginal : RunOriginal;
}

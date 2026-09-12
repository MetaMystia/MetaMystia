using System;

using HarmonyLib;

using NightScene.GuestManagementUtility;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.SpecialGuestsController))]
[AutoLog]
public partial class SpecialGuestsControllerPatch
{
    [HarmonyPatch(nameof(SpecialGuestsController.PostEvaluation))]
    [HarmonyPrefix]
    public static void PostEvaluation_Prefix(SpecialGuestsController __instance,
        GuestGroupController.EvaluationResult evaluationType) =>
        YuyukoGuestSync.BeforePostEvaluation(__instance, evaluationType);

    /// <summary>
    /// 主机或客机用于推进 EatingDelay -> ContinueDecision
    /// </summary>
    /// <param name="__instance"></param>
    /// <exception cref="InvalidOperationException"></exception>
    [HarmonyPatch(nameof(SpecialGuestsController.PostEvaluation))]
    [HarmonyPostfix]
    public static void PostEvaluation_Postfix(SpecialGuestsController __instance)
    {
        if (YuyukoGuestSync.IsBody(__instance)) return;
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        GuestFSM.OnPostEvaluation(__instance);
    }
}

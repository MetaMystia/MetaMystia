using HarmonyLib;
using System;

using NightScene.GuestManagementUtility;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.SpecialGuestsController))]
[AutoLog]
public partial class SpecialGuestsControllerPatch
{
    /// <summary>
    /// 主机或客机用于推进 EatingDelay -> ContinueDecision
    /// </summary>
    /// <param name="__instance"></param>
    /// <exception cref="InvalidOperationException"></exception>
    [HarmonyPatch(nameof(SpecialGuestsController.PostEvaluation))]
    [HarmonyPostfix]
    public static void SpecialGuest_PostEvaluation_Postfix(SpecialGuestsController __instance)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        GuestFSM.OnPostEvaluation(__instance);
    }
}

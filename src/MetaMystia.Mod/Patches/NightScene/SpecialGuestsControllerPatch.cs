using HarmonyLib;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.SpecialGuestsController))]
public class SpecialGuestsControllerPatch
{
    [HarmonyPatch(nameof(SpecialGuestsController.PostEvaluation))]
    [HarmonyPrefix]
    public static void PostEvaluation_Prefix(SpecialGuestsController __instance,
        ref GuestGroupController.EvaluationResult evaluationType)
    {
        EvaluateOrderMessage.BeforePostEvaluation(__instance, ref evaluationType);
        YuyukoGuestSync.BeforePostEvaluation(__instance, evaluationType);
    }
}
